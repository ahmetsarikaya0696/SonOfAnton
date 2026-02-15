using System.Text.RegularExpressions;

namespace SonOfAnton.Backend.Engine.Expressions;

/// <summary>
/// Template expression evaluator — {{variable}} kalıplarını çözer
/// </summary>
public partial class ExpressionEvaluator
{
    /// <summary>
    /// Metin içindeki {{...}} ifadelerini VariableStore'dan resolve eder
    /// </summary>
    public static string Evaluate(string template, VariableStore store)
    {
        if (string.IsNullOrEmpty(template)) return template;

        return TemplatePattern().Replace(template, match =>
        {
            var expression = match.Groups[1].Value.Trim();
            return ResolveExpression(expression, store)?.ToString() ?? match.Value;
        });
    }

    /// <summary>
    /// Parametrelerdeki tüm string değerleri resolve eder
    /// </summary>
    public static Dictionary<string, object?> ResolveParameters(
        Dictionary<string, object?> parameters, VariableStore store)
    {
        var resolved = new Dictionary<string, object?>(parameters.Count);
        foreach (var (key, value) in parameters)
        {
            if (value is string strValue)
                resolved[key] = Evaluate(strValue, store);
            else
                resolved[key] = value;
        }
        return resolved;
    }

    private static object? ResolveExpression(string expression, VariableStore store)
    {
        // $env.KEY — ortam değişkeni
        if (expression.StartsWith("$env."))
        {
            var envKey = expression[5..];
            return Environment.GetEnvironmentVariable(envKey);
        }

        // $node.{nodeId}.output.{field} — başka node'un çıktısı
        if (expression.StartsWith("$node."))
        {
            var parts = expression.Split('.');
            if (parts.Length >= 3)
            {
                var nodeKey = $"__node_{parts[1]}_output";
                var nodeOutput = store.Get(nodeKey);
                if (parts.Length > 3 && nodeOutput is IDictionary<string, object?> dict)
                {
                    return dict.TryGetValue(parts[3], out var fieldValue) ? fieldValue : null;
                }
                return nodeOutput;
            }
        }

        // $index, $item — loop değişkenleri
        if (expression.StartsWith('$'))
        {
            return store.Get(expression);
        }

        // Normal değişken
        return store.Get(expression);
    }

    [GeneratedRegex(@"\{\{(.+?)\}\}")]
    private static partial Regex TemplatePattern();
}
