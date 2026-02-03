using System.Text.Json;

namespace SonOfAnton.Backend.Engine.Handlers;

/// <summary>
/// De�er atama node'u
/// </summary>
public class SetValueHandler : INodeHandler
{
    public string NodeType => "transform.set";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var valuesJson = context.Node.Parameters.GetValueOrDefault("values")?.ToString();
        
        if (string.IsNullOrEmpty(valuesJson))
        {
            return Task.FromResult(NodeExecutionResult.Ok(context.InputData));
        }

        try
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, object?>>(valuesJson);
            context.Log("De�erler atand�", Shared.Enums.LogLevel.Debug, values);
            
            return Task.FromResult(NodeExecutionResult.Ok(values));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeExecutionResult.Fail($"De�er atama hatas�: {ex.Message}"));
        }
    }
}

/// <summary>
/// Filtre node'u
/// </summary>
public class FilterHandler : INodeHandler
{
    public string NodeType => "transform.filter";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var condition = context.Node.Parameters.GetValueOrDefault("condition")?.ToString();
        
        if (string.IsNullOrEmpty(condition))
        {
            return Task.FromResult(NodeExecutionResult.Ok(context.InputData));
        }

        context.Log($"Filtre uygulan�yor: {condition}", Shared.Enums.LogLevel.Debug, null);
        
        // Basit implementasyon - ger�ek uygulamada expression evaluator kullan�lmal�
        // �imdilik veriyi oldu�u gibi ge�ir
        return Task.FromResult(NodeExecutionResult.Ok(context.InputData));
    }
}

/// <summary>
/// Kod �al��t�rma node'u
/// </summary>
public class CodeHandler : INodeHandler
{
    public string NodeType => "transform.code";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var code = context.Node.Parameters.GetValueOrDefault("code")?.ToString();
        
        context.Log("Kod �al��t�r�l�yor", Shared.Enums.LogLevel.Debug, null);
        
        // G�venlik nedeniyle ger�ek kod �al��t�rma yap�lm�yor
        // Ger�ek uygulamada sandboxed JavaScript/Lua kullan�labilir
        return Task.FromResult(NodeExecutionResult.Ok(context.InputData));
    }
}

