using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;

namespace SonOfAnton.Backend.Engine.Handlers;

/// <summary>
/// Veritabaný sorgu node'u
/// SQL injection korumasý ve tehlikeli komut engelleme
/// </summary>
public class DatabaseHandler : INodeHandler
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Engellenen SQL komutlarý (DDL ve admin komutlarý)
    /// </summary>
    private static readonly string[] ForbiddenKeywords =
    [
        "DROP TABLE", "DROP DATABASE", "DROP SCHEMA", "DROP INDEX",
        "ALTER TABLE", "ALTER DATABASE", "ALTER SCHEMA",
        "CREATE TABLE", "CREATE DATABASE", "CREATE SCHEMA",
        "TRUNCATE", "GRANT", "REVOKE", "EXEC ", "EXECUTE ",
        "xp_", "sp_", "OPENROWSET", "OPENDATASOURCE",
        "INTO OUTFILE", "LOAD_FILE", "COPY "
    ];

    private const int MaxResultRows = 1000;
    private const int MaxQueryLength = 5000;

    public DatabaseHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public string NodeType => "data.database";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var operation = context.Node.Parameters.GetValueOrDefault("operation")?.ToString() ?? "select";
        var query = context.Node.Parameters.GetValueOrDefault("query")?.ToString();

        if (string.IsNullOrEmpty(query))
        {
            return NodeExecutionResult.Fail("Sorgu parametresi gerekli");
        }

        if (query.Length > MaxQueryLength)
        {
            return NodeExecutionResult.Fail($"Sorgu çok uzun (max {MaxQueryLength} karakter)");
        }

        var securityCheck = ValidateQuerySecurity(query, operation);
        if (securityCheck != null)
        {
            context.Log($"Güvenlik ihlali: {securityCheck}", Shared.Enums.LogLevel.Error, null);
            return NodeExecutionResult.Fail(securityCheck);
        }

        context.Log($"Veritabaný iþlemi: {operation}", Shared.Enums.LogLevel.Info, null);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();

            switch (operation.ToLowerInvariant())
            {
                case "select":
                    return await ExecuteSelectAsync(dbContext, query, context);

                case "insert":
                case "update":
                case "delete":
                    return await ExecuteNonQueryAsync(dbContext, query, operation, context);

                default:
                    return NodeExecutionResult.Fail($"Bilinmeyen iþlem: {operation}");
            }
        }
        catch (Exception ex)
        {
            context.Log($"Veritabaný hatasý: {ex.Message}", Shared.Enums.LogLevel.Error, null);
            return NodeExecutionResult.Fail($"Veritabaný hatasý: {ex.Message}");
        }
    }

    private static string? ValidateQuerySecurity(string query, string operation)
    {
        var upperQuery = query.ToUpperInvariant();

        foreach (var keyword in ForbiddenKeywords)
        {
            if (upperQuery.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return $"Yasaklý SQL komutu: {keyword}";
            }
        }

        if (ContainsMultipleStatements(query))
        {
            return "Birden fazla SQL ifadesi desteklenmiyor";
        }

        return operation.ToLowerInvariant() switch
        {
            "select" when !upperQuery.TrimStart().StartsWith("SELECT") =>
                "Select iþlemi SELECT ile baþlamalýdýr",
            "insert" when !upperQuery.TrimStart().StartsWith("INSERT") =>
                "Insert iþlemi INSERT ile baþlamalýdýr",
            "update" when !upperQuery.TrimStart().StartsWith("UPDATE") =>
                "Update iþlemi UPDATE ile baþlamalýdýr",
            "delete" when !upperQuery.TrimStart().StartsWith("DELETE") =>
                "Delete iþlemi DELETE ile baþlamalýdýr",
            _ => null
        };
    }

    private static bool ContainsMultipleStatements(string query)
    {
        var inString = false;
        var stringChar = '\0';

        for (int i = 0; i < query.Length; i++)
        {
            var c = query[i];

            if (inString)
            {
                if (c == stringChar && (i + 1 >= query.Length || query[i + 1] != stringChar))
                    inString = false;
            }
            else
            {
                if (c is '\'' or '"')
                {
                    inString = true;
                    stringChar = c;
                }
                else if (c == ';')
                {
                    var remaining = query[(i + 1)..].Trim();
                    if (remaining.Length > 0)
                        return true;
                }
            }
        }

        return false;
    }

    private static async Task<NodeExecutionResult> ExecuteSelectAsync(
        SonOfAntonDbContext dbContext, string query, NodeExecutionContext context)
    {
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(context.CancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = query;
            command.CommandTimeout = 30;

            using var reader = await command.ExecuteReaderAsync(context.CancellationToken);

            var results = new List<Dictionary<string, object?>>();
            var columns = Enumerable.Range(0, reader.FieldCount)
                .Select(i => reader.GetName(i))
                .ToList();

            while (await reader.ReadAsync(context.CancellationToken))
            {
                if (results.Count >= MaxResultRows)
                {
                    context.Log($"Sonuç limiti aþýldý, ilk {MaxResultRows} satýr döndürülüyor",
                        Shared.Enums.LogLevel.Warning, null);
                    break;
                }

                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[columns[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                results.Add(row);
            }

            context.Log($"Sorgu tamamlandý: {results.Count} satýr", Shared.Enums.LogLevel.Info, null);

            return NodeExecutionResult.Ok(new
            {
                rows = results,
                rowCount = results.Count,
                columns,
                truncated = results.Count >= MaxResultRows
            });
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static async Task<NodeExecutionResult> ExecuteNonQueryAsync(
        SonOfAntonDbContext dbContext, string query, string operation, NodeExecutionContext context)
    {
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(context.CancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = query;
            command.CommandTimeout = 30;

            var affectedRows = await command.ExecuteNonQueryAsync(context.CancellationToken);

            context.Log($"{operation} tamamlandý: {affectedRows} satýr etkilendi", Shared.Enums.LogLevel.Info, null);

            return NodeExecutionResult.Ok(new
            {
                operation,
                affectedRows
            });
        }
        finally
        {
            await connection.CloseAsync();
        }
    }
}
