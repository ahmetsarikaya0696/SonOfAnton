namespace SonOfAnton.Shared.Events;

/// <summary>
/// Agent ile UI aras�ndaki ileti�im protokol� i�in event tipleri
/// </summary>
public enum EventType
{
    /// <summary>Log mesajlar� (debug, info, warning)</summary>
    Log,
    
    /// <summary>Proje plan� JSON olarak g�nderildi</summary>
    Plan,
    
    /// <summary>Mermaid diyagram kodu</summary>
    Diagram,
    
    /// <summary>Kod ��kt�s� (dosya yolu ile birlikte)</summary>
    Code,
    
    /// <summary>Task durumu g�ncellendi</summary>
    TaskUpdate,
    
    /// <summary>Hata olu�tu</summary>
    Error,
    
    /// <summary>T�m i�lemler tamamland�</summary>
    Finish
}

