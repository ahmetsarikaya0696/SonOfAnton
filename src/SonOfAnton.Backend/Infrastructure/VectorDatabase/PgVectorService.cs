using System.Text.Json;
using Npgsql;
using Pgvector;
using Pgvector.Npgsql;

namespace SonOfAnton.Backend.Infrastructure.VectorDatabase;

/// <summary>
/// PostgreSQL + pgvector implementasyonu
/// Dapper kullanarak AOT-uyumlu
/// </summary>
public class PgVectorService : IVectorDatabaseService
{
    private readonly string _connectionString;
    private readonly ILogger<PgVectorService> _logger;
    
    public PgVectorService(string connectionString, ILogger<PgVectorService> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }
    
    private async Task<NpgsqlConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(_connectionString);
        dataSourceBuilder.UseVector();
        await using var dataSource = dataSourceBuilder.Build();
        
        var connection = dataSource.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
    
    public async Task CreateCollectionAsync(string collection, int dimension, CancellationToken cancellationToken = default)
    {
        var tableName = SanitizeTableName(collection);
        
        await using var connection = await GetConnectionAsync(cancellationToken);
        
        // pgvector extension'ı etkinleştir
        await using (var cmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector", connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        
        // Tablo oluştur
        var createTableSql = $@"
            CREATE TABLE IF NOT EXISTS {tableName} (
                id TEXT PRIMARY KEY,
                embedding vector({dimension}) NOT NULL,
                metadata JSONB,
                content TEXT,
                created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
            )";
        
        await using (var cmd = new NpgsqlCommand(createTableSql, connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        
        _logger.LogInformation("Collection oluşturuldu: {Collection} (dimension: {Dimension})", collection, dimension);
    }
    
    public async Task<bool> CollectionExistsAsync(string collection, CancellationToken cancellationToken = default)
    {
        var tableName = SanitizeTableName(collection);
        
        await using var connection = await GetConnectionAsync(cancellationToken);
        
        var sql = @"
            SELECT EXISTS (
                SELECT FROM information_schema.tables 
                WHERE table_schema = 'public' 
                AND table_name = @tableName
            )";
        
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("tableName", tableName);
        
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }
    
    public async Task<bool> DeleteCollectionAsync(string collection, CancellationToken cancellationToken = default)
    {
        var tableName = SanitizeTableName(collection);
        
        await using var connection = await GetConnectionAsync(cancellationToken);
        
        var sql = $"DROP TABLE IF EXISTS {tableName}";
        await using var cmd = new NpgsqlCommand(sql, connection);
        
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("Collection silindi: {Collection}", collection);
        
        return true;
    }
    
    public async Task StoreAsync(
        string collection,
        string id,
        float[] vector,
        JsonDocument? metadata = null,
        string? content = null,
        CancellationToken cancellationToken = default)
    {
        var tableName = SanitizeTableName(collection);
        
        await using var connection = await GetConnectionAsync(cancellationToken);
        
        var sql = $@"
            INSERT INTO {tableName} (id, embedding, metadata, content, created_at, updated_at)
            VALUES (@id, @embedding, @metadata, @content, NOW(), NOW())
            ON CONFLICT (id) DO UPDATE SET
                embedding = EXCLUDED.embedding,
                metadata = EXCLUDED.metadata,
                content = EXCLUDED.content,
                updated_at = NOW()";
        
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("embedding", new Vector(vector));
        cmd.Parameters.AddWithValue("metadata", NpgsqlTypes.NpgsqlDbType.Jsonb, 
            metadata != null ? metadata.RootElement.GetRawText() : DBNull.Value);
        cmd.Parameters.AddWithValue("content", (object?)content ?? DBNull.Value);
        
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
    
    public async Task StoreBatchAsync(
        string collection,
        IEnumerable<(string Id, float[] Vector, JsonDocument? Metadata, string? Content)> items,
        CancellationToken cancellationToken = default)
    {
        var tableName = SanitizeTableName(collection);
        var itemsList = items.ToList();
        
        if (itemsList.Count == 0) return;
        
        await using var connection = await GetConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        
        try
        {
            var sql = $@"
                INSERT INTO {tableName} (id, embedding, metadata, content, created_at, updated_at)
                VALUES (@id, @embedding, @metadata, @content, NOW(), NOW())
                ON CONFLICT (id) DO UPDATE SET
                    embedding = EXCLUDED.embedding,
                    metadata = EXCLUDED.metadata,
                    content = EXCLUDED.content,
                    updated_at = NOW()";
            
            foreach (var item in itemsList)
            {
                await using var cmd = new NpgsqlCommand(sql, connection, transaction);
                cmd.Parameters.AddWithValue("id", item.Id);
                cmd.Parameters.AddWithValue("embedding", new Vector(item.Vector));
                cmd.Parameters.AddWithValue("metadata", NpgsqlTypes.NpgsqlDbType.Jsonb,
                    item.Metadata != null ? item.Metadata.RootElement.GetRawText() : DBNull.Value);
                cmd.Parameters.AddWithValue("content", (object?)item.Content ?? DBNull.Value);
                
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            
            await transaction.CommitAsync(cancellationToken);
            _logger.LogDebug("Batch stored: {Count} items to {Collection}", itemsList.Count, collection);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
    
    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collection,
        float[] queryVector,
        int topK = 10,
        double minSimilarity = 0.0,
        SimilarityMetric metric = SimilarityMetric.Cosine,
        JsonDocument? metadataFilter = null,
        CancellationToken cancellationToken = default)
    {
        var tableName = SanitizeTableName(collection);
        
        await using var connection = await GetConnectionAsync(cancellationToken);
        
        // Operatör seçimi
        var (distanceOp, orderBy, scoreCalc) = metric switch
        {
            SimilarityMetric.Cosine => ("<=>", "ASC", "1 - (embedding <=> @query)"),
            SimilarityMetric.Euclidean => ("<->", "ASC", "1 / (1 + (embedding <-> @query))"),
            SimilarityMetric.InnerProduct => ("<#>", "ASC", "-(embedding <#> @query)"),
            _ => ("<=>", "ASC", "1 - (embedding <=> @query)")
        };
        
        var sql = $@"
            SELECT 
                id,
                embedding {distanceOp} @query as distance,
                {scoreCalc} as score,
                metadata,
                content
            FROM {tableName}
            WHERE {scoreCalc} >= @minSimilarity
            ORDER BY embedding {distanceOp} @query {orderBy}
            LIMIT @topK";
        
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("query", new Vector(queryVector));
        cmd.Parameters.AddWithValue("minSimilarity", minSimilarity);
        cmd.Parameters.AddWithValue("topK", topK);
        
        var results = new List<VectorSearchResult>();
        
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new VectorSearchResult
            {
                Id = reader.GetString(0),
                Distance = reader.GetDouble(1),
                Score = reader.GetDouble(2),
                Metadata = reader.IsDBNull(3) ? null : JsonDocument.Parse(reader.GetString(3)),
                Content = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }
        
        return results;
    }
    
    public async Task<VectorSearchResult?> GetByIdAsync(
        string collection,
        string id,
        CancellationToken cancellationToken = default)
    {
        var tableName = SanitizeTableName(collection);
        
        await using var connection = await GetConnectionAsync(cancellationToken);
        
        var sql = $"SELECT id, metadata, content FROM {tableName} WHERE id = @id";
        
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", id);
        
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new VectorSearchResult
            {
                Id = reader.GetString(0),
                Score = 1.0,
                Distance = 0,
                Metadata = reader.IsDBNull(1) ? null : JsonDocument.Parse(reader.GetString(1)),
                Content = reader.IsDBNull(2) ? null : reader.GetString(2)
            };
        }
        
        return null;
    }
    
    public async Task<bool> DeleteAsync(
        string collection,
        string id,
        CancellationToken cancellationToken = default)
    {
        var tableName = SanitizeTableName(collection);
        
        await using var connection = await GetConnectionAsync(cancellationToken);
        
        var sql = $"DELETE FROM {tableName} WHERE id = @id";
        
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", id);
        
        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }
    
    public async Task CreateIndexAsync(
        string collection,
        SimilarityMetric metric = SimilarityMetric.Cosine,
        int m = 16,
        int efConstruction = 64,
        CancellationToken cancellationToken = default)
    {
        var tableName = SanitizeTableName(collection);
        var indexName = $"idx_{tableName}_embedding_hnsw";
        
        var opsClass = metric switch
        {
            SimilarityMetric.Cosine => "vector_cosine_ops",
            SimilarityMetric.Euclidean => "vector_l2_ops",
            SimilarityMetric.InnerProduct => "vector_ip_ops",
            _ => "vector_cosine_ops"
        };
        
        await using var connection = await GetConnectionAsync(cancellationToken);
        
        // Mevcut index'i kaldır (varsa)
        await using (var dropCmd = new NpgsqlCommand($"DROP INDEX IF EXISTS {indexName}", connection))
        {
            await dropCmd.ExecuteNonQueryAsync(cancellationToken);
        }
        
        // HNSW index oluştur
        var sql = $@"
            CREATE INDEX {indexName}
            ON {tableName}
            USING hnsw (embedding {opsClass})
            WITH (m = {m}, ef_construction = {efConstruction})";
        
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.CommandTimeout = 300; // Index oluşturma uzun sürebilir
        
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        
        _logger.LogInformation(
            "HNSW index oluşturuldu: {IndexName} (m={M}, ef_construction={EfConstruction})",
            indexName, m, efConstruction);
    }
    
    /// <summary>
    /// SQL injection önleme için tablo adını temizler
    /// </summary>
    private static string SanitizeTableName(string name)
    {
        // Sadece alfanumerik ve alt çizgi kabul et
        var sanitized = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        
        // embeddings_ prefix'i ekle
        if (!sanitized.StartsWith("embeddings_"))
        {
            sanitized = $"embeddings_{sanitized}";
        }
        
        return sanitized.ToLowerInvariant();
    }
}
