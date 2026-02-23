using System.Text.RegularExpressions;

namespace SonOfAnton.Backend.Engine.Expressions;

/// <summary>
/// Template expression evaluator — {{expression}} kalıplarını çözer
/// Artık fonksiyon çağrıları, operatörler ve karşılaştırmalar desteklenir:
///   {{formatDate(now(); "yyyy-MM-dd")}}
///   {{if(score > 50; "Başarılı"; "Başarısız")}}
///   {{toUpper(name)}}
/// </summary>
public partial class ExpressionEvaluator
{
    /// <summary>
    /// Metin içindeki {{...}} ifadelerini parse ederek değerlendirir
    /// </summary>
    public static string Evaluate(string template, VariableStore store)
    {
        if (string.IsNullOrEmpty(template)) return template;

        return TemplatePattern().Replace(template, match =>
        {
            var expression = match.Groups[1].Value.Trim();
            try
            {
                return ResolveExpression(expression, store)?.ToString() ?? match.Value;
            }
            catch
            {
                // Parse hatası durumunda orijinal ifadeyi koru
                return match.Value;
            }
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

    /// <summary>
    /// İfadeyi parse ve evaluate eder (yeni motor)
    /// </summary>
    private static object? ResolveExpression(string expression, VariableStore store)
    {
        var lexer = new ExpressionLexer(expression);
        var tokens = lexer.Tokenize();
        var parser = new ExpressionParser(tokens);
        var ast = parser.Parse();
        var interpreter = new ExpressionInterpreter(store, FunctionRegistry.Instance);
        return interpreter.Evaluate(ast);
    }

    /// <summary>
    /// İfadeyi doğrular — geçersizse hata döner
    /// </summary>
    public static (bool IsValid, string? Error) Validate(string expression)
    {
        try
        {
            var lexer = new ExpressionLexer(expression);
            var tokens = lexer.Tokenize();
            var parser = new ExpressionParser(tokens);
            parser.Parse();
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    [GeneratedRegex(@"\{\{(.+?)\}\}", RegexOptions.Singleline)]
    private static partial Regex TemplatePattern();
}
