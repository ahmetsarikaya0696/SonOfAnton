using System.Text.Json;

namespace SonOfAnton.Backend.Engine.Handlers;

/// <summary>
/// Koşullu dallanma (IF) node'u
/// </summary>
public class IfHandler : INodeHandler
{
    public string NodeType => "logic.if";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var condition = context.Node.Parameters.GetValueOrDefault("condition")?.ToString();
        
        if (string.IsNullOrEmpty(condition))
        {
            return Task.FromResult(NodeExecutionResult.Fail("Koşul parametresi gerekli"));
        }

        context.Log($"Koşul değerlendiriliyor: {condition}", Shared.Enums.LogLevel.Debug, null);
        
        bool result = EvaluateCondition(condition, context.InputData, context.Memory);
        
        var outputPort = result ? "true" : "false";
        context.Log($"Koşul sonucu: {result} -> Port: {outputPort}", Shared.Enums.LogLevel.Info, null);
        
        return Task.FromResult(NodeExecutionResult.Ok(context.InputData, outputPort));
    }

    private static bool EvaluateCondition(string condition, object? data, Dictionary<string, object?> memory)
    {
        // Boolean değer
        if (bool.TryParse(condition, out var boolResult))
            return boolResult;
        
        // Data null kontrolü
        if (condition.Equals("hasData", StringComparison.OrdinalIgnoreCase))
            return data != null;
        
        if (condition.Equals("isEmpty", StringComparison.OrdinalIgnoreCase))
            return data == null || data.ToString() == string.Empty;
        
        // JSON property kontrolü
        if (data is JsonElement json)
        {
            // "property == value" formatı
            if (condition.Contains("=="))
            {
                var parts = condition.Split("==").Select(p => p.Trim()).ToArray();
                if (parts.Length == 2)
                {
                    var propValue = GetJsonPropertyValue(json, parts[0]);
                    return propValue == parts[1];
                }
            }
            
            // "property != value" formatı
            if (condition.Contains("!="))
            {
                var parts = condition.Split("!=").Select(p => p.Trim()).ToArray();
                if (parts.Length == 2)
                {
                    var propValue = GetJsonPropertyValue(json, parts[0]);
                    return propValue != parts[1];
                }
            }
            
            // "property > value" formatı (sayısal)
            if (condition.Contains('>') && !condition.Contains(">="))
            {
                var parts = condition.Split('>').Select(p => p.Trim()).ToArray();
                if (parts.Length == 2)
                {
                    var propValue = GetJsonPropertyValue(json, parts[0]);
                    if (double.TryParse(propValue, out var left) && double.TryParse(parts[1], out var right))
                        return left > right;
                }
            }
            
            // "property < value" formatı (sayısal)
            if (condition.Contains('<') && !condition.Contains("<="))
            {
                var parts = condition.Split('<').Select(p => p.Trim()).ToArray();
                if (parts.Length == 2)
                {
                    var propValue = GetJsonPropertyValue(json, parts[0]);
                    if (double.TryParse(propValue, out var left) && double.TryParse(parts[1], out var right))
                        return left < right;
                }
            }
            
            // Property varlık kontrolü
            if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty(condition, out _))
                return true;
        }
        
        // Varsayılan
        return true;
    }
    
    private static string GetJsonPropertyValue(JsonElement json, string propertyPath)
    {
        var current = json;
        foreach (var part in propertyPath.Split('.'))
        {
            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out var next))
            {
                current = next;
            }
            else
            {
                return string.Empty;
            }
        }
        return current.ToString();
    }
}

/// <summary>
/// Switch/Router node'u - değere göre farklı portlara yönlendirir
/// </summary>
public class SwitchHandler : INodeHandler
{
    public string NodeType => "logic.switch";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var valueExpr = context.Node.Parameters.GetValueOrDefault("value")?.ToString();
        
        if (string.IsNullOrEmpty(valueExpr))
        {
            return Task.FromResult(NodeExecutionResult.Ok(context.InputData, "output0"));
        }
        
        // Input data'dan değeri çıkar
        string resolvedValue = ResolveValue(valueExpr, context.InputData);
        
        context.Log($"Yönlendirme değeri: {resolvedValue}", Shared.Enums.LogLevel.Debug, null);
        
        // Eşleşen case'i bul
        var cases = context.Node.Parameters
            .Where(p => p.Key.StartsWith("case_"))
            .ToDictionary(p => p.Key.Replace("case_", "output"), p => p.Value?.ToString() ?? "");
        
        foreach (var (outputPort, caseValue) in cases)
        {
            if (resolvedValue.Equals(caseValue, StringComparison.OrdinalIgnoreCase))
            {
                context.Log($"Eşleşme: {caseValue} -> {outputPort}", Shared.Enums.LogLevel.Info, null);
                return Task.FromResult(NodeExecutionResult.Ok(context.InputData, outputPort));
            }
        }
        
        // Sayısal eşleşme: değer 0,1,2... ise output0, output1, output2... 
        if (int.TryParse(resolvedValue, out var index) && index >= 0 && index <= 9)
        {
            var port = $"output{index}";
            context.Log($"Sayısal yönlendirme: {index} -> {port}", Shared.Enums.LogLevel.Info, null);
            return Task.FromResult(NodeExecutionResult.Ok(context.InputData, port));
        }
        
        // Varsayılan port
        context.Log("Eşleşme bulunamadı, varsayılan: output0", Shared.Enums.LogLevel.Info, null);
        return Task.FromResult(NodeExecutionResult.Ok(context.InputData, "output0"));
    }
    
    private static string ResolveValue(string expression, object? data)
    {
        if (data is JsonElement json && json.ValueKind == JsonValueKind.Object)
        {
            if (json.TryGetProperty(expression, out var prop))
            {
                return prop.ToString();
            }
        }
        return expression;
    }
}

/// <summary>
/// Merge node'u - birden fazla girişi birleştirir
/// </summary>
public class MergeHandler : INodeHandler
{
    public string NodeType => "logic.merge";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        context.Log("Girdiler birleştiriliyor", Shared.Enums.LogLevel.Debug, null);
        
        // Gelen veriyi olduğu gibi geçir
        return Task.FromResult(NodeExecutionResult.Ok(context.InputData));
    }
}

/// <summary>
/// Delay/Wait node'u - belirtilen süre bekler
/// </summary>
public class DelayHandler : INodeHandler
{
    public string NodeType => "logic.delay";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var delayMs = 1000; // varsayılan 1 saniye
        
        if (context.Node.Parameters.TryGetValue("delay", out var delayParam))
        {
            if (int.TryParse(delayParam?.ToString(), out var parsed))
            {
                delayMs = parsed;
            }
        }
        
        context.Log($"Bekleniyor: {delayMs}ms", Shared.Enums.LogLevel.Info, null);
        
        await Task.Delay(delayMs, context.CancellationToken);
        
        context.Log("Bekleme tamamlandı", Shared.Enums.LogLevel.Info, null);
        
        return NodeExecutionResult.Ok(context.InputData);
    }
}

/// <summary>
/// Loop/Iterator node'u - array/list üzerinde döngü yapar
/// </summary>
public class LoopHandler : INodeHandler
{
    public string NodeType => "logic.loop";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        context.Log("Döngü başlatılıyor", Shared.Enums.LogLevel.Info, null);
        
        // Array input'u kontrol et
        if (context.InputData is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.Array)
            {
                var items = json.EnumerateArray().ToList();
                context.Log($"Döngü: {items.Count} öğe üzerinde iterasyon", Shared.Enums.LogLevel.Info, null);
                
                // Her item'ı output olarak geçir (workflow runner tarafından sırayla işlenecek)
                var results = items.Select((item, index) => new
                {
                    index,
                    item,
                    totalItems = items.Count
                }).ToList();
                
                return Task.FromResult(NodeExecutionResult.Ok(results));
            }
        }
        
        // Tekil veriyi array olarak sarmalı
        return Task.FromResult(NodeExecutionResult.Ok(new[] { context.InputData }));
    }
}


