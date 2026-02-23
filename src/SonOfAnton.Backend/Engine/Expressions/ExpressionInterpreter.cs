using System.Text.Json;

namespace SonOfAnton.Backend.Engine.Expressions;

/// <summary>
/// AST üzerinden ifadeyi değerlendiren interpreter
/// </summary>
public class ExpressionInterpreter
{
    private readonly VariableStore _store;
    private readonly FunctionRegistry _functions;

    public ExpressionInterpreter(VariableStore store, FunctionRegistry functions)
    {
        _store = store;
        _functions = functions;
    }

    /// <summary>
    /// AST düğümünü değerlendir ve sonucu döndür
    /// </summary>
    public object? Evaluate(AstNode node)
    {
        return node switch
        {
            LiteralNode lit => lit.Value,
            VariableNode var => ResolveVariable(var.Name),
            FunctionCallNode fn => EvaluateFunction(fn),
            PropertyAccessNode pa => EvaluatePropertyAccess(pa),
            BinaryOpNode bin => EvaluateBinaryOp(bin),
            UnaryOpNode un => EvaluateUnaryOp(un),
            IndexAccessNode idx => EvaluateIndexAccess(idx),
            _ => throw new InvalidOperationException($"Bilinmeyen AST düğümü: {node.GetType().Name}")
        };
    }

    // ─── Variable Resolution ───────────────────────────────

    private object? ResolveVariable(string name)
    {
        // $env.KEY
        if (name.StartsWith("$env."))
        {
            return Environment.GetEnvironmentVariable(name[5..]);
        }

        // $node.{id}.output.{field}
        if (name.StartsWith("$node."))
        {
            var parts = name.Split('.');
            if (parts.Length >= 3)
            {
                var nodeKey = $"__node_{parts[1]}_output";
                var nodeOutput = _store.Get(nodeKey);
                if (parts.Length > 3 && nodeOutput is IDictionary<string, object?> dict)
                {
                    return dict.TryGetValue(parts[3], out var fieldValue) ? fieldValue : null;
                }

                // JsonElement erişimi
                if (parts.Length > 3 && nodeOutput is JsonElement je)
                {
                    return TraverseJsonElement(je, parts[3..]);
                }

                return nodeOutput;
            }
        }

        // $ ile başlayan özel değişkenler ($item, $index, vs.)
        if (name.StartsWith('$'))
        {
            return _store.Get(name);
        }

        // Normal değişken
        return _store.Get(name);
    }

    private static object? TraverseJsonElement(JsonElement element, string[] path)
    {
        var current = element;
        foreach (var part in path)
        {
            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out var prop))
            {
                current = prop;
            }
            else
            {
                return null;
            }
        }
        return JsonElementToObject(current);
    }

    private static object? JsonElementToObject(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => el.GetRawText()
        };
    }

    // ─── Function Call ─────────────────────────────────────

    private object? EvaluateFunction(FunctionCallNode fn)
    {
        var args = fn.Arguments.Select(Evaluate).ToArray();
        return _functions.Invoke(fn.FunctionName, args);
    }

    // ─── Property Access ───────────────────────────────────

    private object? EvaluatePropertyAccess(PropertyAccessNode pa)
    {
        var obj = Evaluate(pa.Object);
        if (obj == null) return null;

        // Dictionary<string, object?>
        if (obj is IDictionary<string, object?> dict)
        {
            return dict.TryGetValue(pa.Property, out var val) ? val : null;
        }

        // JsonElement
        if (obj is JsonElement je && je.ValueKind == JsonValueKind.Object)
        {
            if (je.TryGetProperty(pa.Property, out var prop))
                return JsonElementToObject(prop);
            return null;
        }

        // Reflection fallback
        var propInfo = obj.GetType().GetProperty(pa.Property);
        return propInfo?.GetValue(obj);
    }

    // ─── Index Access ──────────────────────────────────────

    private object? EvaluateIndexAccess(IndexAccessNode idx)
    {
        var obj = Evaluate(idx.Object);
        var index = Evaluate(idx.Index);

        if (obj == null) return null;

        if (obj is IList<object?> list && index is long i)
            return i >= 0 && i < list.Count ? list[(int)i] : null;

        if (obj is JsonElement je && je.ValueKind == JsonValueKind.Array && index is long arrIndex)
        {
            var arr = je.EnumerateArray().ToList();
            return arrIndex >= 0 && arrIndex < arr.Count ? JsonElementToObject(arr[(int)arrIndex]) : null;
        }

        if (obj is IDictionary<string, object?> dict && index != null)
            return dict.TryGetValue(index.ToString()!, out var val) ? val : null;

        return null;
    }

    // ─── Binary Operations ─────────────────────────────────

    private object? EvaluateBinaryOp(BinaryOpNode bin)
    {
        // Kısa devre değerlendirme (short-circuit)
        if (bin.Operator == "&&")
        {
            var leftVal = Evaluate(bin.Left);
            if (!IsTruthy(leftVal)) return false;
            return IsTruthy(Evaluate(bin.Right));
        }

        if (bin.Operator == "||")
        {
            var leftVal = Evaluate(bin.Left);
            if (IsTruthy(leftVal)) return true;
            return IsTruthy(Evaluate(bin.Right));
        }

        var left = Evaluate(bin.Left);
        var right = Evaluate(bin.Right);

        return bin.Operator switch
        {
            "+" => Add(left, right),
            "-" => Arithmetic(left, right, (a, b) => a - b),
            "*" => Arithmetic(left, right, (a, b) => a * b),
            "/" => Arithmetic(left, right, (a, b) => b != 0 ? a / b : 0),
            "%" => Arithmetic(left, right, (a, b) => b != 0 ? a % b : 0),
            "==" => Equals(left, right),
            "!=" => !Equals(left, right),
            "<" => Compare(left, right) < 0,
            ">" => Compare(left, right) > 0,
            "<=" => Compare(left, right) <= 0,
            ">=" => Compare(left, right) >= 0,
            _ => throw new InvalidOperationException($"Bilinmeyen operatör: {bin.Operator}")
        };
    }

    private static object? Add(object? left, object? right)
    {
        // String birleştirme
        if (left is string || right is string)
            return $"{left}{right}";

        return Arithmetic(left, right, (a, b) => a + b);
    }

    private static object? Arithmetic(object? left, object? right, Func<double, double, double> op)
    {
        var a = ToDouble(left);
        var b = ToDouble(right);
        if (a == null || b == null) return null;
        return op(a.Value, b.Value);
    }

    private static new bool Equals(object? left, object? right)
    {
        if (left == null && right == null) return true;
        if (left == null || right == null) return false;

        // Sayısal karşılaştırma
        var a = ToDouble(left);
        var b = ToDouble(right);
        if (a != null && b != null) return Math.Abs(a.Value - b.Value) < 0.0001;

        return left.ToString() == right.ToString();
    }

    private static int Compare(object? left, object? right)
    {
        var a = ToDouble(left);
        var b = ToDouble(right);
        if (a != null && b != null) return a.Value.CompareTo(b.Value);

        return string.Compare(left?.ToString(), right?.ToString(), StringComparison.Ordinal);
    }

    // ─── Unary Operations ──────────────────────────────────

    private object? EvaluateUnaryOp(UnaryOpNode un)
    {
        var operand = Evaluate(un.Operand);

        return un.Operator switch
        {
            "!" => !IsTruthy(operand),
            "-" => ToDouble(operand) is { } d ? -d : null,
            _ => throw new InvalidOperationException($"Bilinmeyen tekli operatör: {un.Operator}")
        };
    }

    // ─── Helpers ───────────────────────────────────────────

    internal static bool IsTruthy(object? value)
    {
        if (value == null) return false;
        if (value is bool b) return b;
        if (value is string s) return !string.IsNullOrEmpty(s);
        if (value is double d) return d != 0;
        if (value is long l) return l != 0;
        if (value is int i) return i != 0;
        return true;
    }

    internal static double? ToDouble(object? value)
    {
        if (value == null) return null;
        if (value is double d) return d;
        if (value is long l) return l;
        if (value is int i) return i;
        if (value is float f) return f;
        if (value is decimal dec) return (double)dec;
        if (value is string s && double.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return null;
    }
}
