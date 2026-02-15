namespace SonOfAnton.Backend.Execution;

/// <summary>
/// CancellationToken benzeri ama iptal etmeden duraklatma mekanizması.
/// ManualResetEventSlim tabanlı.
/// </summary>
public class PauseToken
{
    private readonly ManualResetEventSlim _pauseEvent = new(true); // başlangıçta "devam" durumunda
    private volatile bool _isPaused;

    /// <summary>
    /// Duraklatılmış mı
    /// </summary>
    public bool IsPaused => _isPaused;

    /// <summary>
    /// Duraklat
    /// </summary>
    public void Pause()
    {
        _isPaused = true;
        _pauseEvent.Reset();
    }

    /// <summary>
    /// Devam ettir
    /// </summary>
    public void Resume()
    {
        _isPaused = false;
        _pauseEvent.Set();
    }

    /// <summary>
    /// Duraklatılmışsa bekle, değilse hemen devam et.
    /// CancellationToken ile birlikte kullanılabilir.
    /// </summary>
    public async Task WaitIfPausedAsync(CancellationToken cancellationToken = default)
    {
        if (!_isPaused) return;

        await Task.Run(() =>
        {
            _pauseEvent.Wait(cancellationToken);
        }, cancellationToken);
    }
}

/// <summary>
/// Execution ID → PauseToken eşlemesi (Singleton)
/// </summary>
public class PauseTokenRegistry
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, PauseToken> _tokens = new();

    public PauseToken GetOrCreate(Guid executionId)
    {
        return _tokens.GetOrAdd(executionId, _ => new PauseToken());
    }

    public void Remove(Guid executionId)
    {
        _tokens.TryRemove(executionId, out _);
    }

    public PauseToken? Get(Guid executionId)
    {
        return _tokens.TryGetValue(executionId, out var token) ? token : null;
    }

    public void PauseExecution(Guid executionId)
    {
        if (_tokens.TryGetValue(executionId, out var token))
            token.Pause();
    }

    public void ResumeExecution(Guid executionId)
    {
        if (_tokens.TryGetValue(executionId, out var token))
            token.Resume();
    }
}
