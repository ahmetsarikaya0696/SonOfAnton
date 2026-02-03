using System.Text.Json;

namespace SonOfAnton.Backend.Engine.Handlers;

/// <summary>
/// Ko�ullu dallanma (IF) node'u
/// </summary>
public class IfHandler : INodeHandler
{
    public string NodeType => "logic.if";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var condition = context.Node.Parameters.GetValueOrDefault("condition")?.ToString();
        
        if (string.IsNullOrEmpty(condition))
        {
            return Task.FromResult(NodeExecutionResult.Fail("Ko�ul parametresi gerekli"));
        }

        context.Log($"Ko�ul de�erlendiriliyor: {condition}", Shared.Enums.LogLevel.Debug, null);
        
        // Basit ko�ul de�erlendirmesi
        bool result = EvaluateSimpleCondition(condition, context.InputData);
        
        var outputPort = result ? "true" : "false";
        context.Log($"Ko�ul sonucu: {result} -> Port: {outputPort}", Shared.Enums.LogLevel.Info, null);
        
        return Task.FromResult(NodeExecutionResult.Ok(context.InputData, outputPort));
    }

    private static bool EvaluateSimpleCondition(string condition, object? data)
    {
        // Basit boolean de�erlendirmesi
        if (bool.TryParse(condition, out var boolResult))
            return boolResult;
        
        // Data'dan de�er kontrol�
        if (data is JsonElement json)
        {
            // "statusCode == 200" gibi basit ko�ullar i�in
            if (condition.Contains("=="))
            {
                var parts = condition.Split("==").Select(p => p.Trim()).ToArray();
                if (parts.Length == 2 && json.TryGetProperty(parts[0], out var prop))
                {
                    return prop.ToString() == parts[1];
                }
            }
        }
        
        // Varsay�lan olarak true
        return true;
    }
}

/// <summary>
/// Switch/Router node'u
/// </summary>
public class SwitchHandler : INodeHandler
{
    public string NodeType => "logic.switch";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var valueExpr = context.Node.Parameters.GetValueOrDefault("value")?.ToString();
        
        context.Log($"Y�nlendirme de�eri: {valueExpr}", Shared.Enums.LogLevel.Debug, null);
        
        // Basit implementasyon - varsay�lan olarak output0'a y�nlendir
        return Task.FromResult(NodeExecutionResult.Ok(context.InputData, "output0"));
    }
}

/// <summary>
/// Merge node'u - birden fazla giri�i birle�tirir
/// </summary>
public class MergeHandler : INodeHandler
{
    public string NodeType => "logic.merge";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        context.Log("Girdiler birle�tiriliyor", Shared.Enums.LogLevel.Debug, null);
        
        // Gelen veriyi oldu�u gibi ge�ir
        return Task.FromResult(NodeExecutionResult.Ok(context.InputData));
    }
}

