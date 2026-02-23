using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Web;
using SonOfAnton.Shared.Models;

namespace SonOfAnton.Backend.Engine.Expressions;

/// <summary>
/// Tüm hazır inline fonksiyonları barındıran registry
/// Singleton olarak kullanılır
/// </summary>
public class FunctionRegistry
{
    private static readonly Lazy<FunctionRegistry> _instance = new(() => new FunctionRegistry());
    public static FunctionRegistry Instance => _instance.Value;

    private readonly Dictionary<string, Func<object?[], object?>> _functions = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<InlineFunction> _metadata = [];

    private FunctionRegistry()
    {
        RegisterStringFunctions();
        RegisterDateFunctions();
        RegisterNumberFunctions();
        RegisterLogicFunctions();
        RegisterCollectionFunctions();
        RegisterConvertFunctions();
    }

    /// <summary>
    /// Fonksiyonu çağır
    /// </summary>
    public object? Invoke(string name, object?[] args)
    {
        if (!_functions.TryGetValue(name, out var func))
            throw new InvalidOperationException($"Bilinmeyen fonksiyon: {name}");

        return func(args);
    }

    /// <summary>
    /// Fonksiyon var mı
    /// </summary>
    public bool Has(string name) => _functions.ContainsKey(name);

    /// <summary>
    /// Tüm fonksiyon metadata listesi
    /// </summary>
    public IReadOnlyList<InlineFunction> GetAllFunctions() => _metadata;

    // ═════════════════════════════════════════════════════════
    //  STRING FUNCTIONS
    // ═════════════════════════════════════════════════════════

    private void RegisterStringFunctions()
    {
        Register("substring", "String", "Metnin bir bölümünü keser",
            "substring(\"Merhaba Dünya\"; 0; 7) → \"Merhaba\"",
            [P("text", "string"), P("start", "number"), P("length", "number", false)],
            "string",
            args =>
            {
                var text = Str(args, 0);
                var start = IntN(args, 1);
                if (text == null || start == null) return null;
                var s = start.Value;
                if (args.Length > 2)
                {
                    var len = IntN(args, 2) ?? (text.Length - s);
                    return text.Substring(Math.Min(s, text.Length), Math.Min(len, text.Length - s));
                }
                return s < text.Length ? text[s..] : "";
            });

        Register("toUpper", "String", "Metni büyük harfe çevirir",
            "toUpper(\"hello\") → \"HELLO\"",
            [P("text", "string")], "string",
            args => Str(args, 0)?.ToUpperInvariant());

        Register("toLower", "String", "Metni küçük harfe çevirir",
            "toLower(\"HELLO\") → \"hello\"",
            [P("text", "string")], "string",
            args => Str(args, 0)?.ToLowerInvariant());

        Register("trim", "String", "Metnin baş ve sonundaki boşlukları siler",
            "trim(\"  hello  \") → \"hello\"",
            [P("text", "string")], "string",
            args => Str(args, 0)?.Trim());

        Register("replace", "String", "Metindeki bir kalıbı değiştirir",
            "replace(\"hello world\"; \"world\"; \"dünya\") → \"hello dünya\"",
            [P("text", "string"), P("search", "string"), P("replacement", "string")], "string",
            args =>
            {
                var text = Str(args, 0);
                var search = Str(args, 1);
                var replacement = Str(args, 2);
                if (text == null || search == null) return text;
                return text.Replace(search, replacement ?? "");
            });

        Register("length", "String", "Metnin karakter uzunluğunu döner",
            "length(\"hello\") → 5",
            [P("text", "string")], "number",
            args => Str(args, 0)?.Length ?? 0);

        Register("contains", "String", "Metnin içinde bir kalıp var mı kontrol eder",
            "contains(\"hello world\"; \"world\") → true",
            [P("text", "string"), P("search", "string")], "boolean",
            args =>
            {
                var text = Str(args, 0);
                var search = Str(args, 1);
                return text?.Contains(search ?? "", StringComparison.OrdinalIgnoreCase) ?? false;
            });

        Register("split", "String", "Metni ayracla böler",
            "split(\"a,b,c\"; \",\") → [\"a\", \"b\", \"c\"]",
            [P("text", "string"), P("separator", "string")], "array",
            args =>
            {
                var text = Str(args, 0);
                var sep = Str(args, 1);
                if (text == null || sep == null) return Array.Empty<string>();
                return text.Split(sep).Cast<object?>().ToList();
            });

        Register("join", "String", "Diziyi ayraçla birleştirir",
            "join([\"a\"; \"b\"; \"c\"]; \",\") → \"a,b,c\"",
            [P("array", "array"), P("separator", "string")], "string",
            args =>
            {
                var list = ToList(args.Length > 0 ? args[0] : null);
                var sep = Str(args, 1) ?? ",";
                return string.Join(sep, list.Select(x => x?.ToString() ?? ""));
            });

        Register("startsWith", "String", "Metin belirtilen kalıpla başlıyor mu",
            "startsWith(\"hello\"; \"hel\") → true",
            [P("text", "string"), P("prefix", "string")], "boolean",
            args => Str(args, 0)?.StartsWith(Str(args, 1) ?? "", StringComparison.OrdinalIgnoreCase) ?? false);

        Register("endsWith", "String", "Metin belirtilen kalıpla bitiyor mu",
            "endsWith(\"hello\"; \"llo\") → true",
            [P("text", "string"), P("suffix", "string")], "boolean",
            args => Str(args, 0)?.EndsWith(Str(args, 1) ?? "", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    // ═════════════════════════════════════════════════════════
    //  DATE FUNCTIONS
    // ═════════════════════════════════════════════════════════

    private void RegisterDateFunctions()
    {
        Register("now", "Date", "Şu anki tarih/saati döner",
            "now() → \"2024-01-15T10:30:00\"",
            [], "string",
            _ => DateTime.UtcNow.ToString("o"));

        Register("formatDate", "Date", "Tarihi belirtilen formatta biçimlendirir",
            "formatDate(now(); \"yyyy-MM-dd\") → \"2024-01-15\"",
            [P("date", "string"), P("format", "string")], "string",
            args =>
            {
                var dateStr = Str(args, 0);
                var format = Str(args, 1) ?? "yyyy-MM-dd";
                if (dateStr == null) return null;
                if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    return dt.ToString(format, CultureInfo.InvariantCulture);
                return dateStr;
            });

        Register("addDays", "Date", "Tarihe gün ekler",
            "addDays(now(); 7) → \"2024-01-22T10:30:00\"",
            [P("date", "string"), P("days", "number")], "string",
            args =>
            {
                var dateStr = Str(args, 0);
                var days = Dbl(args, 1);
                if (dateStr == null || days == null) return null;
                if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    return dt.AddDays(days.Value).ToString("o");
                return dateStr;
            });

        Register("addHours", "Date", "Tarihe saat ekler",
            "addHours(now(); 3) → \"2024-01-15T13:30:00\"",
            [P("date", "string"), P("hours", "number")], "string",
            args =>
            {
                var dateStr = Str(args, 0);
                var hours = Dbl(args, 1);
                if (dateStr == null || hours == null) return null;
                if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    return dt.AddHours(hours.Value).ToString("o");
                return dateStr;
            });

        Register("parseDate", "Date", "Metni tarih olarak ayrıştırır",
            "parseDate(\"15/01/2024\"; \"dd/MM/yyyy\") → \"2024-01-15\"",
            [P("text", "string"), P("format", "string", false)], "string",
            args =>
            {
                var text = Str(args, 0);
                var format = args.Length > 1 ? Str(args, 1) : null;
                if (text == null) return null;
                if (format != null)
                {
                    if (DateTime.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                        return dt.ToString("o");
                }
                else
                {
                    if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                        return dt.ToString("o");
                }
                return null;
            });

        Register("dateDiff", "Date", "İki tarih arasındaki farkı gün olarak döner",
            "dateDiff(\"2024-01-01\"; \"2024-01-15\") → 14",
            [P("date1", "string"), P("date2", "string"), P("unit", "string", false)], "number",
            args =>
            {
                var d1 = Str(args, 0);
                var d2 = Str(args, 1);
                var unit = args.Length > 2 ? Str(args, 2) : "days";
                if (d1 == null || d2 == null) return null;
                if (!DateTime.TryParse(d1, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt1)) return null;
                if (!DateTime.TryParse(d2, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt2)) return null;
                var diff = dt2 - dt1;
                return (unit?.ToLowerInvariant()) switch
                {
                    "hours" => diff.TotalHours,
                    "minutes" => diff.TotalMinutes,
                    "seconds" => diff.TotalSeconds,
                    _ => diff.TotalDays
                };
            });
    }

    // ═════════════════════════════════════════════════════════
    //  NUMBER FUNCTIONS
    // ═════════════════════════════════════════════════════════

    private void RegisterNumberFunctions()
    {
        Register("round", "Number", "Sayıyı yuvarlar",
            "round(3.7) → 4",
            [P("value", "number"), P("decimals", "number", false)], "number",
            args =>
            {
                var v = Dbl(args, 0);
                if (v == null) return null;
                var decimals = args.Length > 1 ? (IntN(args, 1) ?? 0) : 0;
                return Math.Round(v.Value, decimals, MidpointRounding.AwayFromZero);
            });

        Register("ceil", "Number", "Sayıyı yukarı yuvarlar",
            "ceil(3.2) → 4",
            [P("value", "number")], "number",
            args => Dbl(args, 0) is { } v ? Math.Ceiling(v) : null);

        Register("floor", "Number", "Sayıyı aşağı yuvarlar",
            "floor(3.8) → 3",
            [P("value", "number")], "number",
            args => Dbl(args, 0) is { } v ? Math.Floor(v) : null);

        Register("abs", "Number", "Sayının mutlak değerini döner",
            "abs(-5) → 5",
            [P("value", "number")], "number",
            args => Dbl(args, 0) is { } v ? Math.Abs(v) : null);

        Register("min", "Number", "Verilen sayılardan en küçüğünü döner",
            "min(3; 7; 1; 5) → 1",
            [P("values", "number", true)], "number",
            args =>
            {
                var nums = args.Select(a => ExpressionInterpreter.ToDouble(a)).Where(n => n.HasValue).Select(n => n!.Value);
                return nums.Any() ? nums.Min() : null;
            });

        Register("max", "Number", "Verilen sayılardan en büyüğünü döner",
            "max(3; 7; 1; 5) → 7",
            [P("values", "number", true)], "number",
            args =>
            {
                var nums = args.Select(a => ExpressionInterpreter.ToDouble(a)).Where(n => n.HasValue).Select(n => n!.Value);
                return nums.Any() ? nums.Max() : null;
            });

        Register("toNumber", "Number", "Değeri sayıya çevirir",
            "toNumber(\"42\") → 42",
            [P("value", "any")], "number",
            args => ExpressionInterpreter.ToDouble(args.Length > 0 ? args[0] : null));
    }

    // ═════════════════════════════════════════════════════════
    //  LOGIC FUNCTIONS
    // ═════════════════════════════════════════════════════════

    private void RegisterLogicFunctions()
    {
        Register("if", "Logic", "Koşula göre değer döner",
            "if(score > 50; \"Başarılı\"; \"Başarısız\")",
            [P("condition", "boolean"), P("then", "any"), P("else", "any", false)], "any",
            args =>
            {
                var condition = args.Length > 0 ? ExpressionInterpreter.IsTruthy(args[0]) : false;
                if (condition)
                    return args.Length > 1 ? args[1] : null;
                return args.Length > 2 ? args[2] : null;
            });

        Register("ifEmpty", "Logic", "Değer boşsa alternatifi döner",
            "ifEmpty(name; \"Bilinmiyor\")",
            [P("value", "any"), P("default", "any")], "any",
            args =>
            {
                var value = args.Length > 0 ? args[0] : null;
                if (value == null) return args.Length > 1 ? args[1] : null;
                if (value is string s && string.IsNullOrEmpty(s)) return args.Length > 1 ? args[1] : null;
                return value;
            });

        Register("switch", "Logic", "Değere göre eşleşen sonucu döner (case1; result1; case2; result2; ... ; default)",
            "switch(status; \"active\"; \"Aktif\"; \"passive\"; \"Pasif\"; \"Bilinmiyor\")",
            [P("value", "any"), P("cases", "any", true)], "any",
            args =>
            {
                if (args.Length < 2) return null;
                var value = args[0]?.ToString();
                // case-result çiftleri
                for (int i = 1; i + 1 < args.Length; i += 2)
                {
                    if (args[i]?.ToString() == value)
                        return args[i + 1];
                }
                // Son tek argüman default
                if (args.Length % 2 == 0)
                    return args[^1];
                return null;
            });

        Register("and", "Logic", "Tüm argümanlar doğru mu",
            "and(true; true; false) → false",
            [P("values", "boolean", true)], "boolean",
            args => args.All(ExpressionInterpreter.IsTruthy));

        Register("or", "Logic", "Argümanlardan en az biri doğru mu",
            "or(false; true; false) → true",
            [P("values", "boolean", true)], "boolean",
            args => args.Any(ExpressionInterpreter.IsTruthy));

        Register("not", "Logic", "Mantıksal tersini döner",
            "not(false) → true",
            [P("value", "boolean")], "boolean",
            args => !ExpressionInterpreter.IsTruthy(args.Length > 0 ? args[0] : null));
    }

    // ═════════════════════════════════════════════════════════
    //  COLLECTION FUNCTIONS
    // ═════════════════════════════════════════════════════════

    private void RegisterCollectionFunctions()
    {
        Register("first", "Collection", "Dizinin ilk elemanını döner",
            "first([1; 2; 3]) → 1",
            [P("array", "array")], "any",
            args =>
            {
                var list = ToList(args.Length > 0 ? args[0] : null);
                return list.Count > 0 ? list[0] : null;
            });

        Register("last", "Collection", "Dizinin son elemanını döner",
            "last([1; 2; 3]) → 3",
            [P("array", "array")], "any",
            args =>
            {
                var list = ToList(args.Length > 0 ? args[0] : null);
                return list.Count > 0 ? list[^1] : null;
            });

        Register("count", "Collection", "Dizinin eleman sayısını döner",
            "count([1; 2; 3]) → 3",
            [P("array", "array")], "number",
            args =>
            {
                var list = ToList(args.Length > 0 ? args[0] : null);
                return list.Count;
            });

        Register("flatten", "Collection", "İç içe dizileri düzleştirir",
            "flatten([[1; 2]; [3; 4]]) → [1, 2, 3, 4]",
            [P("array", "array")], "array",
            args =>
            {
                var list = ToList(args.Length > 0 ? args[0] : null);
                var result = new List<object?>();
                foreach (var item in list)
                {
                    if (item is IList<object?> inner)
                        result.AddRange(inner);
                    else
                        result.Add(item);
                }
                return result;
            });

        Register("sort", "Collection", "Diziyi sıralar",
            "sort([3; 1; 2]) → [1, 2, 3]",
            [P("array", "array")], "array",
            args =>
            {
                var list = ToList(args.Length > 0 ? args[0] : null);
                return list.OrderBy(x =>
                {
                    var d = ExpressionInterpreter.ToDouble(x);
                    return d.HasValue ? d.Value : double.MaxValue;
                }).Cast<object?>().ToList();
            });

        Register("unique", "Collection", "Dizideki tekrarlayan elemanları kaldırır",
            "unique([1; 2; 2; 3]) → [1, 2, 3]",
            [P("array", "array")], "array",
            args =>
            {
                var list = ToList(args.Length > 0 ? args[0] : null);
                return list.Distinct().ToList();
            });

        Register("reverse", "Collection", "Diziyi tersine çevirir",
            "reverse([1; 2; 3]) → [3, 2, 1]",
            [P("array", "array")], "array",
            args =>
            {
                var list = ToList(args.Length > 0 ? args[0] : null);
                var reversed = new List<object?>(list);
                reversed.Reverse();
                return reversed;
            });

        Register("slice", "Collection", "Dizinin bir bölümünü alır",
            "slice([1; 2; 3; 4; 5]; 1; 3) → [2, 3, 4]",
            [P("array", "array"), P("start", "number"), P("end", "number", false)], "array",
            args =>
            {
                var list = ToList(args.Length > 0 ? args[0] : null);
                var start = IntN(args, 1) ?? 0;
                var end = args.Length > 2 ? (IntN(args, 2) ?? list.Count) : list.Count;
                start = Math.Max(0, Math.Min(start, list.Count));
                end = Math.Max(start, Math.Min(end, list.Count));
                return list.GetRange(start, end - start);
            });
    }

    // ═════════════════════════════════════════════════════════
    //  CONVERT FUNCTIONS
    // ═════════════════════════════════════════════════════════

    private void RegisterConvertFunctions()
    {
        Register("toString", "Convert", "Değeri metne çevirir",
            "toString(42) → \"42\"",
            [P("value", "any")], "string",
            args => args.Length > 0 ? args[0]?.ToString() : null);

        Register("toJSON", "Convert", "Değeri JSON metnine çevirir",
            "toJSON(data) → \"{...}\"",
            [P("value", "any")], "string",
            args =>
            {
                if (args.Length == 0) return null;
                try { return JsonSerializer.Serialize(args[0]); }
                catch { return args[0]?.ToString(); }
            });

        Register("parseJSON", "Convert", "JSON metnini nesneye çevirir",
            "parseJSON('{\"a\": 1}') → {a: 1}",
            [P("json", "string")], "object",
            args =>
            {
                var text = Str(args, 0);
                if (text == null) return null;
                try
                {
                    var doc = JsonDocument.Parse(text);
                    return doc.RootElement.Clone();
                }
                catch { return null; }
            });

        Register("base64Encode", "Convert", "Metni Base64 olarak kodlar",
            "base64Encode(\"hello\") → \"aGVsbG8=\"",
            [P("text", "string")], "string",
            args =>
            {
                var text = Str(args, 0);
                return text == null ? null : Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
            });

        Register("base64Decode", "Convert", "Base64 metnini çözer",
            "base64Decode(\"aGVsbG8=\") → \"hello\"",
            [P("base64", "string")], "string",
            args =>
            {
                var text = Str(args, 0);
                if (text == null) return null;
                try { return Encoding.UTF8.GetString(Convert.FromBase64String(text)); }
                catch { return null; }
            });

        Register("urlEncode", "Convert", "Metni URL-safe olarak kodlar",
            "urlEncode(\"hello world\") → \"hello+world\"",
            [P("text", "string")], "string",
            args => Str(args, 0) is { } t ? HttpUtility.UrlEncode(t) : null);

        Register("urlDecode", "Convert", "URL-kodlanmış metni çözer",
            "urlDecode(\"hello+world\") → \"hello world\"",
            [P("text", "string")], "string",
            args => Str(args, 0) is { } t ? HttpUtility.UrlDecode(t) : null);

        Register("coalesce", "Convert", "İlk null olmayan değeri döner",
            "coalesce(null; \"\"; \"hello\") → \"hello\"",
            [P("values", "any", true)], "any",
            args =>
            {
                foreach (var arg in args)
                {
                    if (arg != null && !(arg is string s && string.IsNullOrEmpty(s)))
                        return arg;
                }
                return null;
            });

        Register("typeof", "Convert", "Değerin tipini döner",
            "typeof(42) → \"number\"",
            [P("value", "any")], "string",
            args =>
            {
                if (args.Length == 0 || args[0] == null) return "null";
                var v = args[0];
                if (v is string) return "string";
                if (v is bool) return "boolean";
                if (v is double or long or int or float or decimal) return "number";
                if (v is IList<object?>) return "array";
                if (v is JsonElement je) return je.ValueKind switch
                {
                    JsonValueKind.String => "string",
                    JsonValueKind.Number => "number",
                    JsonValueKind.True or JsonValueKind.False => "boolean",
                    JsonValueKind.Array => "array",
                    JsonValueKind.Object => "object",
                    _ => "null"
                };
                return "object";
            });
    }

    // ═════════════════════════════════════════════════════════
    //  REGISTRATION HELPER
    // ═════════════════════════════════════════════════════════

    private void Register(
        string name, string category, string description, string example,
        List<FunctionParameter> parameters, string returnType,
        Func<object?[], object?> implementation)
    {
        _functions[name] = implementation;
        _metadata.Add(new InlineFunction
        {
            Name = name,
            Category = category,
            Description = description,
            Example = example,
            Parameters = parameters,
            ReturnType = returnType
        });
    }

    // ─── Argument Extraction Helpers ───────────────────────

    private static FunctionParameter P(string name, string type, bool required = true)
        => new() { Name = name, Type = type, Required = required };

    private static string? Str(object?[] args, int index)
        => index < args.Length ? args[index]?.ToString() : null;

    private static double? Dbl(object?[] args, int index)
        => index < args.Length ? ExpressionInterpreter.ToDouble(args[index]) : null;

    private static int? IntN(object?[] args, int index)
    {
        var d = Dbl(args, index);
        return d.HasValue ? (int)d.Value : null;
    }

    private static List<object?> ToList(object? value)
    {
        if (value is IList<object?> list) return new List<object?>(list);
        if (value is List<object?> lst) return lst;
        if (value is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            return je.EnumerateArray().Select(e => (object?)JsonElementToObject(e)).ToList();
        }
        if (value is IEnumerable<object> enumerable)
            return enumerable.Cast<object?>().ToList();
        return [];
    }

    private static object? JsonElementToObject(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        _ => el.Clone()
    };
}
