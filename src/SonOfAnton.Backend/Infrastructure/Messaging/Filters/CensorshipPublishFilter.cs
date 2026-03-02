using MassTransit;
using Microsoft.Extensions.Logging;
using SonOfAnton.Shared.Contracts;
using SonOfAnton.Shared.Core;
using SonOfAnton.Shared.Exceptions;

namespace SonOfAnton.Backend.Infrastructure.Messaging.Filters;

public class CensorshipPublishFilter<T> : IFilter<PublishContext<T>> where T : class
{
    private readonly ICensorshipEngine _censorshipEngine;
    private readonly ILogger<CensorshipPublishFilter<T>> _logger;

    public CensorshipPublishFilter(ICensorshipEngine censorshipEngine, ILogger<CensorshipPublishFilter<T>> logger)
    {
        _censorshipEngine = censorshipEngine;
        _logger = logger;
    }

    public async Task Send(PublishContext<T> context, IPipe<PublishContext<T>> next)
    {
        if (context.Message is IMessageWithText messageWithText && !string.IsNullOrWhiteSpace(messageWithText.TextContent))
        {
            var result = await _censorshipEngine.AnalyzeAsync(messageWithText.TextContent, cancellationToken: context.CancellationToken);
            if (result.HasViolation)
            {
                _logger.LogWarning("Censorship violation detected during publish (Egress). Action blocked. Matches: {Matches}, NER: {NerEntities}", 
                    result.SensitiveDataMatches.Count, result.NerEntities.Count);
                throw new CensorshipViolationException(result, "Sensitive data detected during publish (Egress).");
            }
        }

        await next.Send(context);
    }

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("censorship-publish");
    }
}
