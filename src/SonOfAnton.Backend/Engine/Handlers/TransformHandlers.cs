using System.Text.Json;
using System.Text.RegularExpressions;

namespace SonOfAnton.Backend.Engine.Handlers;

/// <summary>
/// Değer atama node'u
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
            
            // Input data ile merge
            if (context.InputData is JsonElement inputJson && inputJson.ValueKind == JsonValueKind.Object)
            {
                var merged = new Dictionary<string, object?>();
                foreach (var prop in inputJson.EnumerateObject())
                {
                    merged[prop.Name] = prop.Value;
                }
                if (values != null)
                {
                    foreach (var (key, value) in values)
                    {
                        merged[key] = value;
                    }
                }
                context.Log("Değerler atandı (merge)", Shared.Enums.LogLevel.Debug, merged);
                return Task.FromResult(NodeExecutionResult.Ok(merged));
            }
            
            context.Log("Değerler atandı", Shared.Enums.LogLevel.Debug, values);
            return Task.FromResult(NodeExecutionResult.Ok(values));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeExecutionResult.Fail($"Değer atama hatası: {ex.Message}"));
        }
    }
}

/// <summary>
/// Filtre node'u - koşullara göre veriyi filtreler
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

        context.Log($"Filtre uygulanıyor: {condition}", Shared.Enums.LogLevel.Debug, null);
        
        if (context.InputData is JsonElement json)
        {
            // Array filtresi
            if (json.ValueKind == JsonValueKind.Array)
            {
                var filtered = json.EnumerateArray()
                    .Where(item => EvaluateFilterCondition(condition, item))
                    .ToList();
                
                context.Log($"Filtre sonucu: {json.GetArrayLength()} -> {filtered.Count} öğe", 
                    Shared.Enums.LogLevel.Info, null);
                
                return Task.FromResult(NodeExecutionResult.Ok(filtered));
            }
            
            // Tek nesne filtresi
            if (json.ValueKind == JsonValueKind.Object)
            {
                if (EvaluateFilterCondition(condition, json))
                {
                    return Task.FromResult(NodeExecutionResult.Ok(context.InputData));
                }
                else
                {
                    context.Log("Filtre: nesne koşulu karşılamadı, boş sonuç", Shared.Enums.LogLevel.Info, null);
                    return Task.FromResult(NodeExecutionResult.Ok((object?)null));
                }
            }
        }
        
        return Task.FromResult(NodeExecutionResult.Ok(context.InputData));
    }
    
    private static bool EvaluateFilterCondition(string condition, JsonElement item)
    {
        // "property == value" formatı
        if (condition.Contains("=="))
        {
            var parts = condition.Split("==").Select(p => p.Trim()).ToArray();
            if (parts.Length == 2 && item.TryGetProperty(parts[0], out var prop))
            {
                return prop.ToString() == parts[1];
            }
        }
        
        // "property > value" formatı 
        if (condition.Contains('>'))
        {
            var parts = condition.Split('>').Select(p => p.Trim()).ToArray();
            if (parts.Length == 2 && item.TryGetProperty(parts[0], out var prop))
            {
                if (double.TryParse(prop.ToString(), out var left) && double.TryParse(parts[1], out var right))
                    return left > right;
            }
        }
        
        // "property < value" formatı
        if (condition.Contains('<'))
        {
            var parts = condition.Split('<').Select(p => p.Trim()).ToArray();
            if (parts.Length == 2 && item.TryGetProperty(parts[0], out var prop))
            {
                if (double.TryParse(prop.ToString(), out var left) && double.TryParse(parts[1], out var right))
                    return left < right;
            }
        }
        
        // Property varlık kontrolü
        if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty(condition, out var exists))
        {
            return exists.ValueKind != JsonValueKind.Null;
        }
        
        return true;
    }
}

/// <summary>
/// Kod çalıştırma node'u - güvenli expression evaluation
/// </summary>
public class CodeHandler : INodeHandler
{
    public string NodeType => "transform.code";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var code = context.Node.Parameters.GetValueOrDefault("code")?.ToString();
        
        if (string.IsNullOrEmpty(code) || code == "return items;")
        {
            return Task.FromResult(NodeExecutionResult.Ok(context.InputData));
        }
        
        context.Log("Kod çalıştırılıyor", Shared.Enums.LogLevel.Debug, null);
        
        try
        {
            // Güvenli expression evaluator
            var result = EvaluateExpression(code, context.InputData, context.Memory);
            context.Log("Kod çalıştırıldı", Shared.Enums.LogLevel.Info, null);
            return Task.FromResult(NodeExecutionResult.Ok(result));
        }
        catch (Exception ex)
        {
            context.Log($"Kod hatası: {ex.Message}", Shared.Enums.LogLevel.Error, null);
            return Task.FromResult(NodeExecutionResult.Fail($"Kod çalıştırma hatası: {ex.Message}"));
        }
    }
    
    private static object? EvaluateExpression(string code, object? inputData, Dictionary<string, object?> memory)
    {
        // JSON transform komutları
        if (code.StartsWith("pick:"))
        {
            // pick:field1,field2 - belirli alanları seç
            var fields = code[5..].Split(',').Select(f => f.Trim()).ToArray();
            return PickFields(inputData, fields);
        }
        
        if (code.StartsWith("omit:"))
        {
            // omit:field1,field2 - belirli alanları çıkar
            var fields = code[5..].Split(',').Select(f => f.Trim()).ToArray();
            return OmitFields(inputData, fields);
        }
        
        if (code.StartsWith("map:"))
        {
            // map:oldKey=newKey,oldKey2=newKey2 - alanları yeniden adlandır
            var mappings = code[4..].Split(',')
                .Select(m => m.Split('='))
                .Where(m => m.Length == 2)
                .ToDictionary(m => m[0].Trim(), m => m[1].Trim());
            return MapFields(inputData, mappings);
        }
        
        if (code.StartsWith("get:"))
        {
            // get:path.to.field - iç içe alana eriş
            var path = code[4..].Trim();
            return GetNestedField(inputData, path);
        }
        
        if (code == "keys")
        {
            // Nesnenin key'lerini döndür
            if (inputData is JsonElement json && json.ValueKind == JsonValueKind.Object)
            {
                return json.EnumerateObject().Select(p => p.Name).ToList();
            }
        }
        
        if (code == "values")
        {
            // Nesnenin değerlerini döndür
            if (inputData is JsonElement json && json.ValueKind == JsonValueKind.Object)
            {
                return json.EnumerateObject().Select(p => p.Value.ToString()).ToList();
            }
        }
        
        if (code == "count" || code == "length")
        {
            if (inputData is JsonElement json)
            {
                if (json.ValueKind == JsonValueKind.Array)
                    return json.GetArrayLength();
                if (json.ValueKind == JsonValueKind.String)
                    return json.GetString()?.Length ?? 0;
            }
        }
        
        if (code == "flatten")
        {
            if (inputData is JsonElement json && json.ValueKind == JsonValueKind.Array)
            {
                var items = new List<JsonElement>();
                foreach (var item in json.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Array)
                    {
                        items.AddRange(item.EnumerateArray());
                    }
                    else
                    {
                        items.Add(item);
                    }
                }
                return items;
            }
        }
        
        if (code == "reverse")
        {
            if (inputData is JsonElement json && json.ValueKind == JsonValueKind.Array)
            {
                return json.EnumerateArray().Reverse().ToList();
            }
        }
        
        // Bilinmeyen kod - veriyi olduğu gibi geçir
        return inputData;
    }
    
    private static object? PickFields(object? data, string[] fields)
    {
        if (data is JsonElement json && json.ValueKind == JsonValueKind.Object)
        {
            var result = new Dictionary<string, object?>();
            foreach (var field in fields)
            {
                if (json.TryGetProperty(field, out var value))
                {
                    result[field] = value;
                }
            }
            return result;
        }
        return data;
    }
    
    private static object? OmitFields(object? data, string[] fields)
    {
        if (data is JsonElement json && json.ValueKind == JsonValueKind.Object)
        {
            var fieldsSet = new HashSet<string>(fields);
            var result = new Dictionary<string, object?>();
            foreach (var prop in json.EnumerateObject())
            {
                if (!fieldsSet.Contains(prop.Name))
                {
                    result[prop.Name] = prop.Value;
                }
            }
            return result;
        }
        return data;
    }
    
    private static object? MapFields(object? data, Dictionary<string, string> mappings)
    {
        if (data is JsonElement json && json.ValueKind == JsonValueKind.Object)
        {
            var result = new Dictionary<string, object?>();
            foreach (var prop in json.EnumerateObject())
            {
                var key = mappings.TryGetValue(prop.Name, out var newKey) ? newKey : prop.Name;
                result[key] = prop.Value;
            }
            return result;
        }
        return data;
    }
    
    private static object? GetNestedField(object? data, string path)
    {
        if (data is JsonElement json)
        {
            var current = json;
            foreach (var part in path.Split('.'))
            {
                if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out var next))
                {
                    current = next;
                }
                else if (current.ValueKind == JsonValueKind.Array && int.TryParse(part, out var index))
                {
                    if (index >= 0 && index < current.GetArrayLength())
                    {
                        current = current[index];
                    }
                    else return null;
                }
                else return null;
            }
            return current;
        }
        return data;
    }
}

/// <summary>
/// Template node'u - mustache-style template ile veri formatlar
/// </summary>
public class TemplateHandler : INodeHandler
{
    public string NodeType => "transform.template";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var template = context.Node.Parameters.GetValueOrDefault("template")?.ToString();
        
        if (string.IsNullOrEmpty(template))
        {
            return Task.FromResult(NodeExecutionResult.Ok(context.InputData));
        }
        
        context.Log("Template uygulanıyor", Shared.Enums.LogLevel.Debug, null);
        
        try
        {
            var result = ApplyTemplate(template, context.InputData);
            context.Log("Template uygulandı", Shared.Enums.LogLevel.Info, null);
            return Task.FromResult(NodeExecutionResult.Ok(result));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeExecutionResult.Fail($"Template hatası: {ex.Message}"));
        }
    }
    
    private static string ApplyTemplate(string template, object? data)
    {
        if (data == null) return template;
        
        var json = data is JsonElement je ? je : JsonSerializer.SerializeToElement(data);
        
        // {{property}} formatını değiştir
        return Regex.Replace(template, @"\{\{(\w+(?:\.\w+)*)\}\}", match =>
        {
            var path = match.Groups[1].Value;
            var current = json;
            foreach (var part in path.Split('.'))
            {
                if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out var next))
                {
                    current = next;
                }
                else
                {
                    return match.Value; // Eşleşmeyen template'i olduğu gibi bırak
                }
            }
            return current.ToString();
        });
    }
}

/// <summary>
/// Aggregate node'u - array verilerini toplar
/// </summary>
public class AggregateHandler : INodeHandler
{
    public string NodeType => "transform.aggregate";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var operation = context.Node.Parameters.GetValueOrDefault("operation")?.ToString() ?? "count";
        var field = context.Node.Parameters.GetValueOrDefault("field")?.ToString();
        
        context.Log($"Toplama işlemi: {operation}", Shared.Enums.LogLevel.Debug, null);
        
        if (context.InputData is not JsonElement json || json.ValueKind != JsonValueKind.Array)
        {
            return Task.FromResult(NodeExecutionResult.Fail("Girdi bir array olmalı"));
        }
        
        var items = json.EnumerateArray().ToList();
        
        object? result = operation.ToLowerInvariant() switch
        {
            "count" => items.Count,
            "sum" => SumField(items, field),
            "avg" or "average" => items.Count > 0 ? SumField(items, field) / items.Count : 0.0,
            "min" => MinField(items, field),
            "max" => MaxField(items, field),
            "first" => items.FirstOrDefault(),
            "last" => items.LastOrDefault(),
            "distinct" => DistinctField(items, field),
            _ => items.Count
        };
        
        context.Log($"Toplama sonucu: {result}", Shared.Enums.LogLevel.Info, null);
        
        return Task.FromResult(NodeExecutionResult.Ok(new { operation, field, result }));
    }
    
    private static double SumField(List<JsonElement> items, string? field)
    {
        return items.Sum(item =>
        {
            var val = field != null && item.TryGetProperty(field, out var prop) ? prop : item;
            return val.TryGetDouble(out var d) ? d : 0.0;
        });
    }
    
    private static double MinField(List<JsonElement> items, string? field)
    {
        return items.Min(item =>
        {
            var val = field != null && item.TryGetProperty(field, out var prop) ? prop : item;
            return val.TryGetDouble(out var d) ? d : double.MaxValue;
        });
    }
    
    private static double MaxField(List<JsonElement> items, string? field)
    {
        return items.Max(item =>
        {
            var val = field != null && item.TryGetProperty(field, out var prop) ? prop : item;
            return val.TryGetDouble(out var d) ? d : double.MinValue;
        });
    }
    
    private static List<string> DistinctField(List<JsonElement> items, string? field)
    {
        return items
            .Select(item => field != null && item.TryGetProperty(field, out var prop) ? prop.ToString() : item.ToString())
            .Distinct()
            .ToList();
    }
}


