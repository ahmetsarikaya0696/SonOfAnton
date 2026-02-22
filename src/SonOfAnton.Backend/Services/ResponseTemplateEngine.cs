using System.Text.Json;
using System.Text.RegularExpressions;

namespace SonOfAnton.Backend.Services;

/// <summary>
/// Basit, dış bağımlılıksız bir JSON/Dictionary tabanlı template motoru.
/// Webhook response şablonlarını render etmek için kullanılır.
/// Destekler: 
/// - Değişken yerleştirme: {{variable}} veya {{result.data.name}}
/// - Çok basit if blokları: {{if success}}...{{/if}} (sadece bool true/false veya null check)
/// </summary>
public static class ResponseTemplateEngine
{
    private static readonly Regex VariableRegex = new(@"\{\{\s*([\w\.]+)\s*\}\}", RegexOptions.Compiled);
    private static readonly Regex IfBlockRegex = new(@"\{\{\s*if\s+([\w\.]+)\s*\}\}(.*?)\{\{\s*/if\s*\}\}", RegexOptions.Compiled | RegexOptions.Singleline);

    public static string Render(string template, Dictionary<string, object?> context)
    {
        if (string.IsNullOrWhiteSpace(template))
            return string.Empty;

        var result = template;

        // 1. If Bloklarını İşle
        result = IfBlockRegex.Replace(result, match =>
        {
            var conditionPath = match.Groups[1].Value;
            var blockContent = match.Groups[2].Value;

            var conditionValue = GetValueFromPath(context, conditionPath);
            var isTruthy = IsTruthy(conditionValue);

            return isTruthy ? blockContent : string.Empty;
        });

        // 2. Değişkenleri İşle
        result = VariableRegex.Replace(result, match =>
        {
            var path = match.Groups[1].Value;
            
            // Eğer if kullanıldıysa atla, yukarida if temizlenmis olmali ama yine de ihtimale karsi
            if (path.StartsWith("if ") || path == "/if") return match.Value;

            var value = GetValueFromPath(context, path);
            
            if (value == null) return string.Empty;
            
            if (value is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.String) return element.GetString() ?? string.Empty;
                return element.GetRawText();
            }
            
            if (value is string str) return str;
            
            return JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });
        });

        return result;
    }

    private static object? GetValueFromPath(Dictionary<string, object?> context, string path)
    {
        var parts = path.Split('.');
        object? current = context;

        foreach (var part in parts)
        {
            if (current == null) return null;

            if (current is Dictionary<string, object?> dict)
            {
                if (dict.TryGetValue(part, out var val))
                {
                    current = val;
                }
                else
                {
                    return null;
                }
            }
            else if (current is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(part, out var prop))
                {
                    current = prop;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                // Property reflection fallback
                var propInfo = current.GetType().GetProperty(part, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (propInfo != null)
                {
                    current = propInfo.GetValue(current);
                }
                else
                {
                    return null;
                }
            }
        }

        return current;
    }

    private static bool IsTruthy(object? value)
    {
        if (value == null) return false;
        
        if (value is bool b) return b;
        if (value is string s) return !string.IsNullOrWhiteSpace(s);
        if (value is int i) return i != 0;
        
        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => false,
                JsonValueKind.String => !string.IsNullOrWhiteSpace(element.GetString()),
                JsonValueKind.Number => element.GetDouble() != 0,
                JsonValueKind.Object => true,
                JsonValueKind.Array => element.GetArrayLength() > 0,
                _ => false
            };
        }

        return true;
    }
}
