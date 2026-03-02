using SonOfAnton.Shared.Core;

namespace SonOfAnton.Shared.Exceptions;

/// <summary>
/// Sansür kuralı ihlali tespit edildiğinde fırlatılan hata
/// </summary>
public class CensorshipViolationException : Exception
{
    public CensorshipResult Result { get; }

    public CensorshipViolationException(CensorshipResult result, string message = "A censorship violation was detected in the message payload.")
        : base(message)
    {
        Result = result;
    }
}
