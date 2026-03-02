using MassTransit;
using Microsoft.Extensions.Logging;
using SonOfAnton.Shared.Contracts;
using SonOfAnton.Shared.Core;
using SonOfAnton.Shared.Exceptions;

namespace SonOfAnton.Backend.Infrastructure.Messaging.Filters;

public class CensorshipConsumeFilter<T> : IFilter<ConsumeContext<T>> where T : class
{
    private readonly ICensorshipEngine _censorshipEngine;
    private readonly ILogger<CensorshipConsumeFilter<T>> _logger;

    public CensorshipConsumeFilter(ICensorshipEngine censorshipEngine, ILogger<CensorshipConsumeFilter<T>> logger)
    {
        _censorshipEngine = censorshipEngine;
        _logger = logger;
    }

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        if (context.Message is IMessageWithText messageWithText && !string.IsNullOrWhiteSpace(messageWithText.TextContent))
        {
            var result = await _censorshipEngine.AnalyzeAsync(messageWithText.TextContent, cancellationToken: context.CancellationToken);
            if (result.HasViolation)
            {
                _logger.LogWarning("Censorship violation detected during consume (Ingress). Action blocked. Matches: {Matches}, NER: {NerEntities}", 
                    result.SensitiveDataMatches.Count, result.NerEntities.Count);
                throw new CensorshipViolationException(result, "Sensitive data detected during consume (Ingress).");
            }
        }

        await next.Send(context);
    }

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("censorship-consume");
    }
}
