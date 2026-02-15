using System.Text.Json;
using SonOfAnton.Backend.Agents;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Enums;

namespace SonOfAnton.Backend.A2A;

/// <summary>
/// Mevcut AgentService agent'larını A2A arayüzüne saran adapter'lar
/// </summary>
public class AnalystA2AAgent : IA2AAgent
{
    private readonly AgentService _agentService;
    public string AgentId => "analyst";

    public AnalystA2AAgent(AgentService agentService) => _agentService = agentService;

    public AgentCardDto GetCard() => new()
    {
        Id = AgentId,
        Name = "Analyst Agent",
        Description = "Kullanıcı gereksinimlerini analiz eder ve proje planı oluşturur",
        Capabilities = ["analyze", "plan"],
        InputSchema = new AgentSchemaDto
        {
            Type = "object",
            Properties = new() { ["prompt"] = new { type = "string" } }
        }
    };

    public async Task<A2ATaskResult> ProcessAsync(object? input, CancellationToken ct = default)
    {
        var prompt = input?.ToString() ?? string.Empty;
        var plan = await _agentService.AnalyzeAsync(prompt, ct);
        return plan != null
            ? A2ATaskResult.Success(plan)
            : A2ATaskResult.Fail("Plan oluşturulamadı");
    }
}

public class CoderA2AAgent : IA2AAgent
{
    private readonly AgentService _agentService;
    public string AgentId => "coder";

    public CoderA2AAgent(AgentService agentService) => _agentService = agentService;

    public AgentCardDto GetCard() => new()
    {
        Id = AgentId,
        Name = "Coder Agent",
        Description = "Verilen görev için üretim kalitesinde kod yazar",
        Capabilities = ["code-generation"]
    };

    public async Task<A2ATaskResult> ProcessAsync(object? input, CancellationToken ct = default)
    {
        if (input is not JsonElement json)
            return A2ATaskResult.Fail("Geçerli JSON input gerekli");

        var task = json.Deserialize<Shared.Models.DevTask>();
        if (task == null)
            return A2ATaskResult.Fail("DevTask parse edilemedi");

        var code = await _agentService.GenerateCodeAsync(task, [], [], ct);
        return !string.IsNullOrEmpty(code)
            ? A2ATaskResult.Success(code)
            : A2ATaskResult.Fail("Kod üretilemedi");
    }
}

public class AuditorA2AAgent : IA2AAgent
{
    private readonly AgentService _agentService;
    public string AgentId => "auditor";

    public AuditorA2AAgent(AgentService agentService) => _agentService = agentService;

    public AgentCardDto GetCard() => new()
    {
        Id = AgentId,
        Name = "Auditor Agent",
        Description = "Kodu kalite ve güvenlik açısından denetler",
        Capabilities = ["code-review", "audit"]
    };

    public async Task<A2ATaskResult> ProcessAsync(object? input, CancellationToken ct = default)
    {
        if (input is not JsonElement json)
            return A2ATaskResult.Fail("Geçerli JSON input gerekli");

        var code = json.GetProperty("code").GetString() ?? string.Empty;
        var file = json.GetProperty("file").GetString() ?? "unknown.cs";
        var result = await _agentService.AuditCodeAsync(code, file, [], ct);
        return result != null
            ? A2ATaskResult.Success(result)
            : A2ATaskResult.Fail("Denetim başarısız");
    }
}

public class VisualizerA2AAgent : IA2AAgent
{
    private readonly AgentService _agentService;
    public string AgentId => "visualizer";

    public VisualizerA2AAgent(AgentService agentService) => _agentService = agentService;

    public AgentCardDto GetCard() => new()
    {
        Id = AgentId,
        Name = "Visualizer Agent",
        Description = "Proje planlarını Mermaid diyagramına dönüştürür",
        Capabilities = ["diagram", "visualization"],
        OutputSchema = new AgentSchemaDto
        {
            Type = "object",
            Properties = new() { ["mermaid"] = new { type = "string" } }
        }
    };

    public async Task<A2ATaskResult> ProcessAsync(object? input, CancellationToken ct = default)
    {
        if (input is not JsonElement json)
            return A2ATaskResult.Fail("Geçerli JSON input gerekli");

        var plan = json.Deserialize<Shared.Models.ProjectPlan>();
        if (plan == null)
            return A2ATaskResult.Fail("ProjectPlan parse edilemedi");

        var diagram = await _agentService.VisualizeAsync(plan, ct);
        return A2ATaskResult.Success(diagram, [new A2AArtifactDto
        {
            Name = "architecture.mmd",
            Type = ArtifactType.Mermaid,
            Content = diagram
        }]);
    }
}
