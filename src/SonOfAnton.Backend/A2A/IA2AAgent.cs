using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Enums;

namespace SonOfAnton.Backend.A2A;

/// <summary>
/// A2A uyumlu agent kontratı
/// </summary>
public interface IA2AAgent
{
    string AgentId { get; }
    AgentCardDto GetCard();
    Task<A2ATaskResult> ProcessAsync(object? input, CancellationToken ct = default);
}

/// <summary>
/// A2A task sonucu
/// </summary>
public class A2ATaskResult
{
    public A2ATaskState State { get; init; }
    public object? Output { get; init; }
    public List<A2AArtifactDto> Artifacts { get; init; } = [];
    public string? Error { get; init; }

    public static A2ATaskResult Success(object? output, List<A2AArtifactDto>? artifacts = null) =>
        new() { State = A2ATaskState.Completed, Output = output, Artifacts = artifacts ?? [] };

    public static A2ATaskResult Fail(string error) =>
        new() { State = A2ATaskState.Failed, Error = error };
}
