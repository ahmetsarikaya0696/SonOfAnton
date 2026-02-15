using System.Text.Json;
using SonOfAnton.Backend.Engine.Expressions;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Backend.Engine.Handlers;

/// <summary>
/// Loop (ForEach) subflow handler — collection üzerinde döngü
/// </summary>
public class ForEachLoopHandler : INodeHandler
{
    public string NodeType => "subflow.foreach";

    private readonly IServiceProvider _serviceProvider;

    public ForEachLoopHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var maxIterations = context.Node.Parameters.TryGetValue("maxIterations", out var maxVal)
            ? Convert.ToInt32(maxVal)
            : 1000;

        // Collection'ı al
        IEnumerable<object?>? collection = null;

        if (context.InputData is IEnumerable<object?> enumerable)
        {
            collection = enumerable;
        }
        else if (context.InputData is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
        {
            collection = jsonElement.EnumerateArray().Cast<object?>();
        }
        else if (context.Node.Parameters.TryGetValue("collection", out var collParam) && collParam is string collStr)
        {
            var store = new VariableStore();
            foreach (var (k, v) in context.Memory)
                store.Set(k, v);
            var resolved = ExpressionEvaluator.Evaluate(collStr, store);
            try
            {
                var arr = JsonSerializer.Deserialize<List<object?>>(resolved);
                collection = arr;
            }
            catch
            {
                return NodeExecutionResult.Fail($"Collection parse edilemedi: {resolved}");
            }
        }

        if (collection == null)
        {
            return NodeExecutionResult.Fail("Döngü için geçerli bir collection bulunamadı");
        }

        var items = collection.ToList();
        var results = new List<object?>();
        var iteration = 0;

        foreach (var item in items)
        {
            if (iteration >= maxIterations)
            {
                context.Log($"Max iteration ({maxIterations}) aşıldı, döngü durduruluyor",
                    Shared.Enums.LogLevel.Warning, null);
                break;
            }

            // Loop değişkenlerini memory'ye ekle
            context.Memory["$index"] = iteration;
            context.Memory["$item"] = item;
            context.Memory["$isFirst"] = iteration == 0;
            context.Memory["$isLast"] = iteration == items.Count - 1;

            context.Log($"ForEach iteration {iteration + 1}/{items.Count}", Shared.Enums.LogLevel.Debug, null);

            // Child graph varsa çalıştır (placeholder — WorkflowRunner seviyesinde handle edilir)
            results.Add(item);
            iteration++;
        }

        // Loop değişkenlerini temizle
        context.Memory.Remove("$index");
        context.Memory.Remove("$item");
        context.Memory.Remove("$isFirst");
        context.Memory.Remove("$isLast");

        return NodeExecutionResult.Ok(results);
    }
}

/// <summary>
/// While loop handler — koşul sağlandığı sürece tekrar
/// </summary>
public class WhileLoopHandler : INodeHandler
{
    public string NodeType => "subflow.while";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var maxIterations = context.Node.Parameters.TryGetValue("maxIterations", out var maxVal)
            ? Convert.ToInt32(maxVal)
            : 1000;

        var conditionExpr = context.Node.Parameters.TryGetValue("condition", out var cond)
            ? cond?.ToString() ?? "false"
            : "false";

        var store = new VariableStore();
        foreach (var (k, v) in context.Memory)
            store.Set(k, v);

        var iteration = 0;
        var results = new List<object?>();

        while (iteration < maxIterations)
        {
            var resolved = ExpressionEvaluator.Evaluate(conditionExpr, store);
            if (!bool.TryParse(resolved, out var continueLoop) || !continueLoop)
                break;

            context.Memory["$index"] = iteration;
            context.Log($"While iteration {iteration + 1}", Shared.Enums.LogLevel.Debug, null);

            results.Add(context.InputData);
            iteration++;

            await Task.Yield();
        }

        if (iteration >= maxIterations)
        {
            context.Log($"While loop max iteration ({maxIterations}) aşıldı",
                Shared.Enums.LogLevel.Warning, null);
        }

        return NodeExecutionResult.Ok(new { iterations = iteration, results });
    }
}

/// <summary>
/// Parallel subflow handler — dalları paralel çalıştırır
/// </summary>
public class ParallelHandler : INodeHandler
{
    public string NodeType => "subflow.parallel";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var maxConcurrency = context.Node.Parameters.TryGetValue("maxConcurrency", out var mc)
            ? Convert.ToInt32(mc)
            : Environment.ProcessorCount;

        var joinStrategyStr = context.Node.Parameters.TryGetValue("joinStrategy", out var js)
            ? js?.ToString() ?? "WaitAll"
            : "WaitAll";

        var errorPolicyStr = context.Node.Parameters.TryGetValue("errorPolicy", out var ep)
            ? ep?.ToString() ?? "FailFast"
            : "FailFast";

        Enum.TryParse<Shared.Enums.JoinStrategy>(joinStrategyStr, out var joinStrategy);
        Enum.TryParse<Shared.Enums.ParallelErrorPolicy>(errorPolicyStr, out var errorPolicy);

        // Child graph'ın dallarını al (placeholder — child nodes WorkflowRunner seviyesinde yönetilir)
        context.Log($"Parallel node: maxConcurrency={maxConcurrency}, join={joinStrategy}, errorPolicy={errorPolicy}",
            Shared.Enums.LogLevel.Info, null);

        // Placeholder: Paralel dallar WorkflowRunner tarafından handle edilir.
        // Bu handler sadece konfigürasyonu okur ve metadata döndürür.
        await Task.CompletedTask;

        return NodeExecutionResult.Ok(new
        {
            type = "parallel",
            maxConcurrency,
            joinStrategy = joinStrategy.ToString(),
            errorPolicy = errorPolicy.ToString(),
            inputData = context.InputData
        });
    }
}

/// <summary>
/// Break node — loop içinde erken çıkış
/// </summary>
public class BreakHandler : INodeHandler
{
    public string NodeType => "flow.break";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        context.Log("Break: Döngü sonlandırılıyor", Shared.Enums.LogLevel.Info, null);
        return Task.FromResult(NodeExecutionResult.Ok(new { action = "break" }, "break"));
    }
}

/// <summary>
/// Continue node — loop içinde bir sonraki iterasyona atla
/// </summary>
public class ContinueHandler : INodeHandler
{
    public string NodeType => "flow.continue";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        context.Log("Continue: Bir sonraki iterasyona geçiliyor", Shared.Enums.LogLevel.Info, null);
        return Task.FromResult(NodeExecutionResult.Ok(new { action = "continue" }, "continue"));
    }
}
