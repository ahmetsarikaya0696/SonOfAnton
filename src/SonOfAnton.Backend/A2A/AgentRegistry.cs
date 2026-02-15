using System.Collections.Concurrent;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Backend.A2A;

/// <summary>
/// Agent kayıt, keşif ve yaşam döngüsü yönetimi
/// </summary>
public class AgentRegistry
{
    private readonly ConcurrentDictionary<string, IA2AAgent> _agents = new();
    private readonly ILogger<AgentRegistry> _logger;

    public AgentRegistry(ILogger<AgentRegistry> logger)
    {
        _logger = logger;
    }

    public void Register(IA2AAgent agent)
    {
        if (_agents.TryAdd(agent.AgentId, agent))
        {
            _logger.LogInformation("A2A Agent kaydedildi: {AgentId}", agent.AgentId);
        }
    }

    public void Unregister(string agentId)
    {
        _agents.TryRemove(agentId, out _);
    }

    public IA2AAgent? Get(string agentId)
    {
        return _agents.TryGetValue(agentId, out var agent) ? agent : null;
    }

    public IReadOnlyList<AgentCardDto> GetAllCards()
    {
        return _agents.Values.Select(a => a.GetCard()).ToList();
    }

    public IReadOnlyList<string> GetAgentIds()
    {
        return [.. _agents.Keys];
    }
}
