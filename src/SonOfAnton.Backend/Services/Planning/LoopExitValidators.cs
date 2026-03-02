using System.Text.Json;
using System.Text.RegularExpressions;
using SonOfAnton.Shared.Core;
using SonOfAnton.Shared.Enums;

namespace SonOfAnton.Backend.Services.Planning;

/// <summary>
/// Maksimum iterasyon sayısına ulaşıldığında döngüyü sonlandırır.
/// </summary>
public partial class MaxIterationValidator : ILoopExitValidator
{
    public string ValidatorName => "MaxIterationValidator";
    public int Priority => 10;

    public Task<LoopExitResult> ValidateAsync(
        LoopExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.CurrentIteration >= context.MaxIterations)
        {
            return Task.FromResult(LoopExitResult.Exit(
                LoopExitReason.MaxIterations,
                $"Maksimum iterasyon sayısına ulaşıldı: {context.CurrentIteration}/{context.MaxIterations}"));
        }

        return Task.FromResult(LoopExitResult.Continue());
    }
}

/// <summary>
/// progress.md'deki tüm görevler tamamlandığında döngüyü sonlandırır.
/// Markdown checklist formatını ayrıştırarak tamamlanma durumunu kontrol eder.
/// Patterns: [x] = tamamlandı, [ ] = bekliyor, [/] = devam ediyor
/// </summary>
public partial class TaskCompletionValidator : ILoopExitValidator
{
    public string ValidatorName => "TaskCompletionValidator";
    public int Priority => 20;

    // Markdown checklist pattern: - [x], - [ ], - [/], * [x], etc.
    [GeneratedRegex(@"^[\s]*[-*]\s*\[([ x/])\]", RegexOptions.Multiline)]
    private static partial Regex ChecklistPattern();

    public Task<LoopExitResult> ValidateAsync(
        LoopExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.ProgressContent))
        {
            return Task.FromResult(LoopExitResult.Continue());
        }

        var matches = ChecklistPattern().Matches(context.ProgressContent);

        if (matches.Count == 0)
        {
            // Checklist yoksa bu validator karar veremez
            return Task.FromResult(LoopExitResult.Continue());
        }

        var totalTasks = matches.Count;
        var completedTasks = matches.Count(m => m.Groups[1].Value == "x");
        var inProgressTasks = matches.Count(m => m.Groups[1].Value == "/");
        var pendingTasks = totalTasks - completedTasks - inProgressTasks;

        if (pendingTasks == 0 && inProgressTasks == 0 && completedTasks == totalTasks)
        {
            return Task.FromResult(LoopExitResult.Exit(
                LoopExitReason.TaskCompleted,
                $"Tüm görevler tamamlandı: {completedTasks}/{totalTasks}"));
        }

        return Task.FromResult(LoopExitResult.Continue());
    }
}

/// <summary>
/// Belirli bir süre aşıldığında döngüyü sonlandırır.
/// </summary>
public class TimeoutValidator : ILoopExitValidator
{
    public string ValidatorName => "TimeoutValidator";
    public int Priority => 5; // Yüksek öncelik — timeout güvenlik mekanizması

    public Task<LoopExitResult> ValidateAsync(
        LoopExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.Elapsed > context.Timeout)
        {
            return Task.FromResult(LoopExitResult.Exit(
                LoopExitReason.Timeout,
                $"Zaman aşımı: {context.Elapsed.TotalMinutes:F1} dk > {context.Timeout.TotalMinutes:F1} dk limit"));
        }

        return Task.FromResult(LoopExitResult.Continue());
    }
}

/// <summary>
/// JSON expression tabanlı özel koşul değerlendirmesi.
/// ContextJson içindeki değerleri CustomExitCondition ile karşılaştırır.
/// 
/// Desteklenen koşul formatı:
/// { "field": "status", "operator": "equals", "value": "done" }
/// { "field": "errorCount", "operator": "greaterThan", "value": 5 }
/// </summary>
public class CustomConditionValidator : ILoopExitValidator
{
    private readonly ILogger<CustomConditionValidator> _logger;

    public string ValidatorName => "CustomConditionValidator";
    public int Priority => 30;

    public CustomConditionValidator(ILogger<CustomConditionValidator> logger)
    {
        _logger = logger;
    }

    public Task<LoopExitResult> ValidateAsync(
        LoopExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.CustomExitCondition) ||
            string.IsNullOrWhiteSpace(context.ContextJson))
        {
            return Task.FromResult(LoopExitResult.Continue());
        }

        try
        {
            var condition = JsonSerializer.Deserialize<CustomCondition>(
                context.CustomExitCondition,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (condition == null)
            {
                return Task.FromResult(LoopExitResult.Continue());
            }

            using var contextDoc = JsonDocument.Parse(context.ContextJson);
            var root = contextDoc.RootElement;

            if (!root.TryGetProperty(condition.Field, out var fieldValue))
            {
                return Task.FromResult(LoopExitResult.Continue());
            }

            var matches = EvaluateCondition(fieldValue, condition.Operator, condition.Value);

            if (matches)
            {
                return Task.FromResult(LoopExitResult.Exit(
                    LoopExitReason.CustomCondition,
                    $"Özel koşul sağlandı: {condition.Field} {condition.Operator} {condition.Value}"));
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Özel koşul ayrıştırılamadı: {Condition}", context.CustomExitCondition);
        }

        return Task.FromResult(LoopExitResult.Continue());
    }

    private static bool EvaluateCondition(JsonElement fieldValue, string op, JsonElement expectedValue)
    {
        return op.ToLowerInvariant() switch
        {
            "equals" or "eq" => CompareEquals(fieldValue, expectedValue),
            "notequals" or "neq" => !CompareEquals(fieldValue, expectedValue),
            "greaterthan" or "gt" => CompareNumeric(fieldValue, expectedValue) > 0,
            "lessthan" or "lt" => CompareNumeric(fieldValue, expectedValue) < 0,
            "greaterthanorequal" or "gte" => CompareNumeric(fieldValue, expectedValue) >= 0,
            "lessthanorequal" or "lte" => CompareNumeric(fieldValue, expectedValue) <= 0,
            "contains" => fieldValue.GetString()?.Contains(
                expectedValue.GetString() ?? "", StringComparison.OrdinalIgnoreCase) ?? false,
            _ => false
        };
    }

    private static bool CompareEquals(JsonElement a, JsonElement b)
    {
        return a.ValueKind switch
        {
            JsonValueKind.String => string.Equals(
                a.GetString(), b.GetString(), StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Number => a.GetDouble() == b.GetDouble(),
            JsonValueKind.True or JsonValueKind.False => a.GetBoolean() == b.GetBoolean(),
            _ => a.GetRawText() == b.GetRawText()
        };
    }

    private static int CompareNumeric(JsonElement a, JsonElement b)
    {
        if (a.TryGetDouble(out var aVal) && b.TryGetDouble(out var bVal))
        {
            return aVal.CompareTo(bVal);
        }
        return 0;
    }

    private sealed class CustomCondition
    {
        public string Field { get; set; } = string.Empty;
        public string Operator { get; set; } = "equals";
        public JsonElement Value { get; set; }
    }
}
