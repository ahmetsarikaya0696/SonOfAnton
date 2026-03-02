namespace SonOfAnton.Backend.Services.AI;

/// <summary>
/// GPU hızlandırma ve donanım limitleri yapılandırması.
/// appsettings.json → "AI:Hardware" bölümüne bağlanır.
/// </summary>
public class HardwareConfig
{
    /// <summary>
    /// GPU hızlandırma etkin mi? (CUDA/DirectML)
    /// </summary>
    public bool EnableGpuAcceleration { get; set; }

    /// <summary>
    /// Aynı anda çalıştırılabilecek maksimum inference sayısı
    /// </summary>
    public int MaxConcurrentInferences { get; set; } = 4;

    /// <summary>
    /// GPU cihaz numarası (multi-GPU sistemlerde)
    /// </summary>
    public int GpuDeviceId { get; set; }

    /// <summary>
    /// GPU bellek limiti (MB). 0 = sınırsız
    /// </summary>
    public int GpuMemoryLimitMB { get; set; }

    /// <summary>
    /// Tek bir inference isteği için zaman aşımı (saniye)
    /// </summary>
    public int InferenceTimeoutSeconds { get; set; } = 120;
}
