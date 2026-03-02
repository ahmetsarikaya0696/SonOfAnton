using Hangfire;
using SonOfAnton.Shared.Core;
using SonOfAnton.Shared.Enums;

namespace SonOfAnton.Backend.Services.Planning;

/// <summary>
/// Ralph Döngüsü — yinelemeli görev yürütme motoru.
/// BackgroundService olarak sürekli çalışır, aktif döngülerin iterasyonlarını yönetir.
/// Hangfire ile entegre çalışarak güvenilir iş zamanlaması sağlar.
/// </summary>
public class RalphLoopEngine : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RalphLoopEngine> _logger;

    /// <summary>
    /// Aktif döngülerin kontrol aralığı
    /// </summary>
    private TimeSpan PollingInterval =>
        TimeSpan.FromSeconds(_configuration.GetValue("RalphLoop:PollingIntervalSeconds", 10));

    public RalphLoopEngine(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<RalphLoopEngine> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Ralph Döngü Motoru başlatılıyor. Polling aralığı: {Interval}s",
            PollingInterval.TotalSeconds);

        // Hangfire recurring job'ı kaydet (yedek mekanizma olarak)
        RegisterHangfireRecurringJob();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessActiveLoopsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ralph döngü motoru iterasyonunda hata oluştu");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }

        _logger.LogInformation("Ralph Döngü Motoru durduruldu.");
    }

    /// <summary>
    /// Tüm aktif döngüleri işler — her iterasyonda çağrılır
    /// </summary>
    private async Task ProcessActiveLoopsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var stateStore = scope.ServiceProvider.GetRequiredService<ILoopStateStore>();
        var validators = scope.ServiceProvider.GetServices<ILoopExitValidator>()
            .OrderBy(v => v.Priority)
            .ToList();
        var planningService = scope.ServiceProvider.GetRequiredService<IMarkdownPlanningService>();

        var activeLoops = await stateStore.GetActiveLoopsAsync(cancellationToken);

        if (activeLoops.Count == 0) return;

        _logger.LogDebug("Aktif döngü sayısı: {Count}", activeLoops.Count);

        foreach (var loop in activeLoops)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                await ProcessSingleLoopAsync(
                    loop.LoopId, stateStore, validators, planningService, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Döngü işlenirken hata: {LoopId}", loop.LoopId);

                // Hata durumunda döngüyü Failed olarak işaretle
                try
                {
                    await stateStore.CompleteLoopAsync(
                        loop.LoopId, LoopExitReason.Error, ex.Message, cancellationToken);
                }
                catch (Exception innerEx)
                {
                    _logger.LogError(innerEx,
                        "Döngü hata durumuna alınırken ikincil hata: {LoopId}", loop.LoopId);
                }
            }
        }
    }

    /// <summary>
    /// Tek bir döngü iterasyonunu işler
    /// </summary>
    private async Task ProcessSingleLoopAsync(
        string loopId,
        ILoopStateStore stateStore,
        IReadOnlyList<ILoopExitValidator> validators,
        IMarkdownPlanningService planningService,
        CancellationToken cancellationToken)
    {
        var state = await stateStore.GetStateAsync(loopId, cancellationToken);
        if (state == null || state.State != LoopState.Running) return;

        // Heartbeat güncelle
        await stateStore.UpdateHeartbeatAsync(loopId, cancellationToken);

        // Progress.md içeriğini oku (TaskCompletionValidator için)
        var progressDoc = await planningService.ReadDocumentAsync(
            loopId, PlanFileType.Progress, cancellationToken);

        // Yürütme bağlamı oluştur
        var context = new LoopExecutionContext
        {
            LoopId = loopId,
            CurrentIteration = state.CurrentIteration,
            MaxIterations = state.MaxIterations,
            StartedAt = state.StartedAt,
            Timeout = TimeSpan.FromMinutes(
                _configuration.GetValue("RalphLoop:DefaultTimeoutMinutes", 60)),
            ContextJson = state.ContextJson,
            ProgressContent = progressDoc?.Content
        };

        // Tüm validator'ları sırayla çalıştır
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);

            if (result.ShouldExit)
            {
                _logger.LogInformation(
                    "Döngü çıkış koşulu sağlandı: {LoopId}, Validator={Validator}, Reason={Reason}, Message={Message}",
                    loopId, validator.ValidatorName, result.Reason, result.Message);

                await stateStore.CompleteLoopAsync(
                    loopId,
                    result.Reason ?? LoopExitReason.CustomCondition,
                    result.Message,
                    cancellationToken);

                return;
            }
        }

        // Çıkış koşulu sağlanmadı — iterasyonu artır
        var nextIteration = state.CurrentIteration + 1;
        await stateStore.UpdateStateAsync(
            loopId, LoopState.Running, nextIteration, state.ContextJson, cancellationToken);

        _logger.LogDebug(
            "Döngü iterasyonu tamamlandı: {LoopId}, Iteration={Iteration}/{Max}",
            loopId, nextIteration, state.MaxIterations);

        // Hangfire ile sonraki iterasyonu da planla (güvenilirlik)
        BackgroundJob.Schedule(
            () => TriggerLoopIterationAsync(loopId, CancellationToken.None),
            TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Hangfire tarafından çağrılan iterasyon tetikleyici
    /// </summary>
    [AutomaticRetry(Attempts = 2)]
    public async Task TriggerLoopIterationAsync(string loopId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var stateStore = scope.ServiceProvider.GetRequiredService<ILoopStateStore>();
        var state = await stateStore.GetStateAsync(loopId, cancellationToken);

        if (state?.State == LoopState.Running)
        {
            _logger.LogDebug("Hangfire tetiklemesi alındı, döngü aktif: {LoopId}", loopId);
            // BackgroundService poll döngüsü zaten işleyecek, burada sadece heartbeat güncelle
            await stateStore.UpdateHeartbeatAsync(loopId, cancellationToken);
        }
    }

    /// <summary>
    /// Hangfire recurring job'ı — periyodik sağlık kontrolü
    /// </summary>
    private void RegisterHangfireRecurringJob()
    {
        try
        {
            var intervalSeconds = _configuration.GetValue("RalphLoop:PollingIntervalSeconds", 10);
            var cronExpression = $"*/{Math.Max(1, intervalSeconds / 60)} * * * *";

            // En az dakikada bir kontrol
            if (intervalSeconds < 60)
            {
                cronExpression = "* * * * *"; // Her dakika
            }

            RecurringJob.AddOrUpdate<RalphLoopEngine>(
                "ralph-loop-health-check",
                engine => engine.HealthCheckAsync(CancellationToken.None),
                cronExpression,
                new RecurringJobOptions());

            _logger.LogInformation("Ralph Loop Hangfire recurring job kaydedildi: {Cron}", cronExpression);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hangfire recurring job kaydı başarısız (Hangfire başlatılmamış olabilir)");
        }
    }

    /// <summary>
    /// Hangfire recurring health check — stuck döngüleri tespit eder
    /// </summary>
    [AutomaticRetry(Attempts = 1)]
    public async Task HealthCheckAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var stateStore = scope.ServiceProvider.GetRequiredService<ILoopStateStore>();
        var activeLoops = await stateStore.GetActiveLoopsAsync(cancellationToken);

        foreach (var loop in activeLoops)
        {
            var heartbeatAge = DateTime.UtcNow - loop.LastHeartbeatAt;
            var stuckThreshold = TimeSpan.FromMinutes(
                _configuration.GetValue("RalphLoop:StuckThresholdMinutes", 5));

            if (heartbeatAge > stuckThreshold)
            {
                _logger.LogWarning(
                    "Stuck döngü tespit edildi: {LoopId}, Son heartbeat: {Ago} önce",
                    loop.LoopId, heartbeatAge);

                await stateStore.CompleteLoopAsync(
                    loop.LoopId,
                    LoopExitReason.Error,
                    $"Döngü {stuckThreshold.TotalMinutes} dakikadır heartbeat göndermedi (stuck).",
                    cancellationToken);
            }
        }
    }
}
