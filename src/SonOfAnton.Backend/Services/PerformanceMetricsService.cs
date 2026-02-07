using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SonOfAnton.Backend.Services;

/// <summary>
/// Uygulama performans metrikleri ve kullaným analitiði servisi.
/// 
/// Bu servis iki iþ yapar:
/// 1. .NET Metrics API ile standart metrikler üretir (Prometheus/Grafana uyumlu)
/// 2. Bellek içi kullaným istatistikleri tutar (dashboard için)
/// 
/// <para><b>Junior Geliþtiriciler Ýçin:</b></para>
/// <para>
/// "Metrics" = Uygulamanýn saðlýðýný gösteren sayýsal veriler.
/// Örneðin: "Son 1 saatte kaç workflow çalýþtý?", "Ortalama süre ne kadar?"
/// Bu verileri izleyerek sorunlarý erken fark edebiliriz.
/// </para>
/// </summary>
public sealed class PerformanceMetricsService : IDisposable
{
    // ???????????????????????????????????????????????????????????????????
    //  .NET Meters & Instruments (Prometheus/Grafana/OpenTelemetry uyumlu)
    // ???????????????????????????????????????????????????????????????????
    
    private readonly Meter _meter;
    
    /// <summary>Toplam çalýþtýrýlan workflow sayýsý (sürekli artan sayaç).</summary>
    private readonly Counter<long> _workflowExecutionCount;
    
    /// <summary>Baþarýsýz workflow sayýsý.</summary>
    private readonly Counter<long> _workflowFailureCount;
    
    /// <summary>Toplam çalýþtýrýlan node sayýsý.</summary>
    private readonly Counter<long> _nodeExecutionCount;
    
    /// <summary>Þu an çalýþan workflow sayýsý (artýp azalan ölçüm).</summary>
    private readonly UpDownCounter<long> _activeWorkflowCount;
    
    /// <summary>Workflow çalýþma süresi (milisaniye cinsinden daðýlým).</summary>
    private readonly Histogram<double> _workflowDurationMs;
    
    /// <summary>Node çalýþma süresi (milisaniye cinsinden daðýlým).</summary>
    private readonly Histogram<double> _nodeDurationMs;

    // ???????????????????????????????????????????????????????????????????
    //  Bellek Ýçi Ýstatistikler (Dashboard ve /api/metrics endpoint'i için)
    // ???????????????????????????????????????????????????????????????????
    
    private long _totalWorkflows;
    private long _totalNodes;
    private long _totalFailures;
    private long _activeWorkflows;
    
    /// <summary>Node tipi baþýna çalýþma sayýsý (ör: "logic.if" ? 42).</summary>
    private readonly ConcurrentDictionary<string, long> _nodeTypeExecutionCounts = new();
    
    /// <summary>Node tipi baþýna toplam çalýþma süresi (ms).</summary>
    private readonly ConcurrentDictionary<string, double> _nodeTypeTotalDurationMs = new();
    
    /// <summary>Son hata mesajlarý (en fazla 50 kayýt, ring buffer mantýðý).</summary>
    private readonly ConcurrentQueue<ErrorRecord> _recentErrors = new();
    
    /// <summary>Saatlik workflow sayýlarý (son 24 saat).</summary>
    private readonly ConcurrentDictionary<string, long> _hourlyWorkflowCounts = new();
    
    private readonly DateTime _serviceStartTime = DateTime.UtcNow;

    /// <summary>
    /// Yeni bir PerformanceMetricsService oluþturur.
    /// Meter adý "SonOfAnton.Workflows" olarak ayarlanýr.
    /// </summary>
    public PerformanceMetricsService()
    {
        // Meter = Metrik üreten fabrika. Adý izleme araçlarýnda görünür.
        _meter = new Meter("SonOfAnton.Workflows", "1.0.0");
        
        // Counter: Sadece artan sayaç (toplam istek sayýsý gibi)
        _workflowExecutionCount = _meter.CreateCounter<long>(
            "sonofanton.workflow.executions.total",
            unit: "{executions}",
            description: "Toplam workflow çalýþtýrma sayýsý");
        
        _workflowFailureCount = _meter.CreateCounter<long>(
            "sonofanton.workflow.failures.total",
            unit: "{failures}",
            description: "Baþarýsýz workflow sayýsý");
        
        _nodeExecutionCount = _meter.CreateCounter<long>(
            "sonofanton.node.executions.total",
            unit: "{executions}",
            description: "Toplam node çalýþtýrma sayýsý");
        
        // UpDownCounter: Artýp azalan sayaç (aktif baðlantý sayýsý gibi)
        _activeWorkflowCount = _meter.CreateUpDownCounter<long>(
            "sonofanton.workflow.active",
            unit: "{workflows}",
            description: "Þu an çalýþan workflow sayýsý");
        
        // Histogram: Deðerlerin daðýlýmýný ölçer (yanýt süresi gibi)
        _workflowDurationMs = _meter.CreateHistogram<double>(
            "sonofanton.workflow.duration",
            unit: "ms",
            description: "Workflow çalýþma süresi (ms)");
        
        _nodeDurationMs = _meter.CreateHistogram<double>(
            "sonofanton.node.duration",
            unit: "ms",
            description: "Node çalýþma süresi (ms)");
    }

    // ???????????????????????????????????????????????????????????????????
    //  Public Metrikler — WorkflowRunner ve Handler'lar tarafýndan çaðrýlýr
    // ???????????????????????????????????????????????????????????????????

    /// <summary>
    /// Bir workflow çalýþtýrýlmaya baþlandýðýnda çaðrýlýr.
    /// Aktif workflow sayacýný artýrýr.
    /// </summary>
    /// <param name="workflowId">Çalýþtýrýlan workflow'un ID'si.</param>
    public void RecordWorkflowStarted(Guid workflowId)
    {
        _workflowExecutionCount.Add(1, new KeyValuePair<string, object?>("workflow.id", workflowId));
        _activeWorkflowCount.Add(1);
        Interlocked.Increment(ref _totalWorkflows);
        Interlocked.Increment(ref _activeWorkflows);
        
        // Saatlik istatistik
        var hourKey = DateTime.UtcNow.ToString("yyyy-MM-dd-HH");
        _hourlyWorkflowCounts.AddOrUpdate(hourKey, 1, (_, count) => count + 1);
    }

    /// <summary>
    /// Bir workflow baþarýyla tamamlandýðýnda çaðrýlýr.
    /// </summary>
    /// <param name="workflowId">Workflow ID'si.</param>
    /// <param name="durationMs">Toplam çalýþma süresi (milisaniye).</param>
    public void RecordWorkflowCompleted(Guid workflowId, double durationMs)
    {
        _workflowDurationMs.Record(durationMs, new KeyValuePair<string, object?>("workflow.id", workflowId));
        _activeWorkflowCount.Add(-1);
        Interlocked.Decrement(ref _activeWorkflows);
    }

    /// <summary>
    /// Bir workflow baþarýsýz olduðunda çaðrýlýr.
    /// </summary>
    /// <param name="workflowId">Workflow ID'si.</param>
    /// <param name="error">Hata mesajý.</param>
    /// <param name="durationMs">Hata anýna kadar geçen süre (ms).</param>
    public void RecordWorkflowFailed(Guid workflowId, string error, double durationMs)
    {
        _workflowFailureCount.Add(1, new KeyValuePair<string, object?>("workflow.id", workflowId));
        _workflowDurationMs.Record(durationMs, new KeyValuePair<string, object?>("workflow.id", workflowId));
        _activeWorkflowCount.Add(-1);
        Interlocked.Increment(ref _totalFailures);
        Interlocked.Decrement(ref _activeWorkflows);
        
        // Son hatalarý kaydet (max 50)
        _recentErrors.Enqueue(new ErrorRecord(workflowId, error, DateTime.UtcNow));
        while (_recentErrors.Count > 50)
            _recentErrors.TryDequeue(out _);
    }

    /// <summary>
    /// Bir node çalýþtýrýldýðýnda çaðrýlýr.
    /// </summary>
    /// <param name="nodeType">Node tipi (ör: "logic.if").</param>
    /// <param name="durationMs">Çalýþma süresi (ms).</param>
    /// <param name="success">Baþarýlý mý?</param>
    public void RecordNodeExecution(string nodeType, double durationMs, bool success)
    {
        _nodeExecutionCount.Add(1,
            new KeyValuePair<string, object?>("node.type", nodeType),
            new KeyValuePair<string, object?>("node.success", success));
        
        _nodeDurationMs.Record(durationMs,
            new KeyValuePair<string, object?>("node.type", nodeType));
        
        Interlocked.Increment(ref _totalNodes);
        _nodeTypeExecutionCounts.AddOrUpdate(nodeType, 1, (_, count) => count + 1);
        _nodeTypeTotalDurationMs.AddOrUpdate(nodeType, durationMs, (_, total) => total + durationMs);
    }

    // ???????????????????????????????????????????????????????????????????
    //  Dashboard / API için Özet Ýstatistikler
    // ???????????????????????????????????????????????????????????????????

    /// <summary>
    /// Dashboard ve /api/metrics endpoint'i için tüm metriklerin özetini döner.
    /// </summary>
    public MetricsSummary GetSummary()
    {
        // Son 24 saatteki sayýlarý temizle (eski kayýtlarý sil)
        var cutoffHour = DateTime.UtcNow.AddHours(-24).ToString("yyyy-MM-dd-HH");
        foreach (var key in _hourlyWorkflowCounts.Keys)
        {
            if (string.Compare(key, cutoffHour, StringComparison.Ordinal) < 0)
                _hourlyWorkflowCounts.TryRemove(key, out _);
        }

        return new MetricsSummary
        {
            UptimeSeconds = (DateTime.UtcNow - _serviceStartTime).TotalSeconds,
            TotalWorkflowExecutions = Interlocked.Read(ref _totalWorkflows),
            TotalNodeExecutions = Interlocked.Read(ref _totalNodes),
            TotalFailures = Interlocked.Read(ref _totalFailures),
            ActiveWorkflows = Interlocked.Read(ref _activeWorkflows),
            FailureRate = _totalWorkflows > 0
                ? Math.Round((double)_totalFailures / _totalWorkflows * 100, 2)
                : 0,
            NodeTypeStats = _nodeTypeExecutionCounts
                .Select(kvp => new NodeTypeStat
                {
                    NodeType = kvp.Key,
                    ExecutionCount = kvp.Value,
                    AverageDurationMs = _nodeTypeTotalDurationMs.TryGetValue(kvp.Key, out var totalMs)
                        ? Math.Round(totalMs / kvp.Value, 2)
                        : 0
                })
                .OrderByDescending(s => s.ExecutionCount)
                .ToList(),
            HourlyCounts = _hourlyWorkflowCounts
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => new HourlyStat { Hour = kvp.Key, Count = kvp.Value })
                .ToList(),
            RecentErrors = _recentErrors
                .Reverse()
                .Take(10)
                .Select(e => new ErrorSummary
                {
                    WorkflowId = e.WorkflowId,
                    Error = e.Error,
                    OccurredAt = e.OccurredAt
                })
                .ToList()
        };
    }

    public void Dispose() => _meter.Dispose();

    // ???????????????????????????????????????????????????????????????????
    //  Ýç Veri Yapýlarý
    // ???????????????????????????????????????????????????????????????????

    private record ErrorRecord(Guid WorkflowId, string Error, DateTime OccurredAt);
}

// ???????????????????????????????????????????????????????????????????????
//  Özet Modelleri (/api/metrics endpoint'i tarafýndan döner)
// ???????????????????????????????????????????????????????????????????????

/// <summary>
/// Performans metrikleri özet raporu.
/// /api/metrics endpoint'inden dönen JSON bu yapýdadýr.
/// </summary>
public class MetricsSummary
{
    /// <summary>Uygulama çalýþma süresi (saniye).</summary>
    public double UptimeSeconds { get; init; }
    
    /// <summary>Toplam çalýþtýrýlan workflow sayýsý.</summary>
    public long TotalWorkflowExecutions { get; init; }
    
    /// <summary>Toplam çalýþtýrýlan node sayýsý.</summary>
    public long TotalNodeExecutions { get; init; }
    
    /// <summary>Toplam baþarýsýz workflow sayýsý.</summary>
    public long TotalFailures { get; init; }
    
    /// <summary>Þu an çalýþan workflow sayýsý.</summary>
    public long ActiveWorkflows { get; init; }
    
    /// <summary>Hata oraný (yüzde). ör: 2.5 = %2.5</summary>
    public double FailureRate { get; init; }
    
    /// <summary>Node tipi bazlý istatistikler.</summary>
    public List<NodeTypeStat> NodeTypeStats { get; init; } = [];
    
    /// <summary>Son 24 saatlik workflow sayýlarý (saat bazlý).</summary>
    public List<HourlyStat> HourlyCounts { get; init; } = [];
    
    /// <summary>Son 10 hata kaydý.</summary>
    public List<ErrorSummary> RecentErrors { get; init; } = [];
}

/// <summary>Node tipi bazlý istatistik.</summary>
public class NodeTypeStat
{
    /// <summary>Node tipi (ör: "logic.if", "transform.set").</summary>
    public string NodeType { get; init; } = string.Empty;
    
    /// <summary>Toplam çalýþtýrýlma sayýsý.</summary>
    public long ExecutionCount { get; init; }
    
    /// <summary>Ortalama çalýþma süresi (ms).</summary>
    public double AverageDurationMs { get; init; }
}

/// <summary>Saatlik workflow sayýsý.</summary>
public class HourlyStat
{
    /// <summary>Saat dilimi (ör: "2025-01-24-14").</summary>
    public string Hour { get; init; } = string.Empty;
    
    /// <summary>O saatteki toplam workflow sayýsý.</summary>
    public long Count { get; init; }
}

/// <summary>Hata kaydý özeti.</summary>
public class ErrorSummary
{
    /// <summary>Hatalý workflow'un ID'si.</summary>
    public Guid WorkflowId { get; init; }
    
    /// <summary>Hata mesajý.</summary>
    public string Error { get; init; } = string.Empty;
    
    /// <summary>Hata zamaný.</summary>
    public DateTime OccurredAt { get; init; }
}
