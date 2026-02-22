using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.Core.Identity;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Entities;

namespace SonOfAnton.Backend.Engine.Handlers;

/// <summary>
/// Custom node'ları çalıştıran handler.
/// JavaScript script'leri Jint engine ile, C# script'leri basit expression evaluator ile yürütülür.
/// </summary>
public class CustomNodeHandler : INodeHandler
{
    private readonly SonOfAntonDbContext _db;
    private readonly IUserContext _userContext;
    private readonly ILogger<CustomNodeHandler> _logger;

    public string NodeType => "custom.*";

    public CustomNodeHandler(SonOfAntonDbContext db, IUserContext userContext, ILogger<CustomNodeHandler> logger)
    {
        _db = db;
        _userContext = userContext;
        _logger = logger;
    }

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var nodeType = context.Node.Type;
        var tenantId = _userContext.TenantId ?? "default";

        // DB'den custom node tanımını al
        var customNode = await _db.CustomNodeDefinitions
            .FirstOrDefaultAsync(n => n.TenantId == tenantId && n.Type == nodeType);

        if (customNode == null)
        {
            return NodeExecutionResult.Fail($"Custom node tanımı bulunamadı: {nodeType}");
        }

        try
        {
            var result = customNode.ScriptType switch
            {
                CustomNodeScriptType.JavaScript => await ExecuteJavaScriptAsync(customNode.ScriptCode, context),
                CustomNodeScriptType.CSharpScript => await ExecuteCSharpScriptAsync(customNode.ScriptCode, context),
                _ => NodeExecutionResult.Fail($"Desteklenmeyen script tipi: {customNode.ScriptType}")
            };

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Custom node çalıştırma hatası: {Type}", nodeType);
            return NodeExecutionResult.Fail($"Script hatası: {ex.Message}");
        }
    }

    /// <summary>
    /// Custom node script'ini test amaçlı çalıştırır.
    /// </summary>
    public async Task<TestCustomNodeResultDto> TestExecuteAsync(
        CustomNodeDefinitionEntity customNode,
        TestCustomNodeDto testDto)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // Sahte bir context oluştur
            var fakeContext = new NodeExecutionContext
            {
                ExecutionId = Guid.NewGuid(),
                Node = new WorkflowNodeDto
                {
                    Id = Guid.NewGuid(),
                    Type = customNode.Type,
                    Name = customNode.Name,
                    Parameters = testDto.Parameters
                },
                InputData = testDto.Inputs.Count > 0 ? testDto.Inputs : null,
                Log = (msg, level, data) => { /* test sırasında log yoksay */ }
            };

            var result = customNode.ScriptType switch
            {
                CustomNodeScriptType.JavaScript => await ExecuteJavaScriptAsync(customNode.ScriptCode, fakeContext),
                CustomNodeScriptType.CSharpScript => await ExecuteCSharpScriptAsync(customNode.ScriptCode, fakeContext),
                _ => NodeExecutionResult.Fail($"Desteklenmeyen script tipi: {customNode.ScriptType}")
            };

            sw.Stop();
            return new TestCustomNodeResultDto
            {
                Success = result.Success,
                Outputs = result.OutputData is Dictionary<string, object?> dict
                    ? dict
                    : new Dictionary<string, object?> { ["output"] = result.OutputData },
                Error = result.Error,
                ExecutionTimeMs = sw.Elapsed.TotalMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new TestCustomNodeResultDto
            {
                Success = false,
                Error = ex.Message,
                ExecutionTimeMs = sw.Elapsed.TotalMilliseconds
            };
        }
    }

    // ─── JavaScript Execution (Sandbox) ─────────────────────────

    private Task<NodeExecutionResult> ExecuteJavaScriptAsync(string script, NodeExecutionContext context)
    {
        try
        {
            // Jint JavaScript engine — sandbox'lu ortam
            var engine = new Jint.Engine();

            // Input ve parametreleri inject et
            var inputs = context.InputData ?? new Dictionary<string, object?>();
            var parameters = context.Node.Parameters ?? new Dictionary<string, object?>();

            engine.SetValue("inputs", inputs);
            engine.SetValue("parameters", parameters);
            engine.SetValue("memory", context.Memory);

            // console.log desteği
            var logs = new List<string>();
            engine.SetValue("console", new
            {
                log = new Action<object?>(msg => logs.Add(msg?.ToString() ?? "null"))
            });

            // Script'i çalıştır
            var wrappedScript = $"(function() {{ {script} }})()";
            var result = engine.Evaluate(wrappedScript);

            // Sonucu al
            var output = result?.ToObject();

            if (logs.Count > 0)
            {
                context.Log(string.Join("\n", logs), Shared.Enums.LogLevel.Info, null);
            }

            return Task.FromResult(NodeExecutionResult.Ok(output));
        }
        catch (TimeoutException)
        {
            return Task.FromResult(NodeExecutionResult.Fail("Script zaman aşımı (10 saniye)"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeExecutionResult.Fail($"JavaScript hatası: {ex.Message}"));
        }
    }

    // ─── C# Script Execution ────────────────────────────────────

    private async Task<NodeExecutionResult> ExecuteCSharpScriptAsync(string script, NodeExecutionContext context)
    {
        try
        {
            var inputs = context.InputData;
            var parameters = context.Node.Parameters ?? new Dictionary<string, object?>();

            // Roslyn script globals
            var globals = new CSharpScriptGlobals
            {
                inputs = inputs,
                parameters = parameters,
                memory = context.Memory
            };

            var result = await Microsoft.CodeAnalysis.CSharp.Scripting.CSharpScript.EvaluateAsync<object?>(
                script,
                Microsoft.CodeAnalysis.Scripting.ScriptOptions.Default
                    .WithImports("System", "System.Collections.Generic", "System.Linq", "System.Text.Json")
                    .WithReferences(typeof(object).Assembly, typeof(JsonSerializer).Assembly, typeof(Enumerable).Assembly),
                globals);

            return NodeExecutionResult.Ok(result);
        }
        catch (Microsoft.CodeAnalysis.Scripting.CompilationErrorException ex)
        {
            return NodeExecutionResult.Fail($"C# derleme hatası: {string.Join("\n", ex.Diagnostics)}");
        }
        catch (Exception ex)
        {
            return NodeExecutionResult.Fail($"C# script hatası: {ex.Message}");
        }
    }
}

/// <summary>
/// C# script globals — script içinden erişilebilir değişkenler
/// </summary>
public class CSharpScriptGlobals
{
    public object? inputs { get; set; }
    public Dictionary<string, object?> parameters { get; set; } = [];
    public Dictionary<string, object?> memory { get; set; } = [];
}
