namespace SonOfAnton.Shared.Contracts;

/// <summary>
/// Sansür motorunun metin içeriğine erişebilmesi için ortak bir arayüz.
/// Bu sayede MassTransit filter'ları mesaja generic yaklaşıp sansür işlemi uygulayabilir.
/// </summary>
public interface IMessageWithText
{
    string TextContent { get; }
}

/// <summary>
/// Ajan tarafından veri analizi yapılması talebi.
/// </summary>
public record AnalyzeDataCommand(
    Guid AgentId, 
    Guid CorrelationId, 
    string TextContent) : IMessageWithText;

/// <summary>
/// Veri analizi sonucunu bildiren olay.
/// </summary>
public record DataAnalyzedEvent(
    Guid OriginalCommandId, 
    string AnalysisResult);
