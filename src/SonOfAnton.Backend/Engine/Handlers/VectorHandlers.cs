using System.Text.Json;
using SonOfAnton.Backend.Infrastructure.Embeddings;
using SonOfAnton.Backend.Infrastructure.VectorDatabase;

namespace SonOfAnton.Backend.Engine.Handlers;

/// <summary>
/// GenerateEmbedding Node Handler
/// Metin veya görüntüyü vektöre dönüştürür
/// </summary>
public class GenerateEmbeddingHandler : INodeHandler
{
    public string NodeType => "generate_embedding";
    
    private readonly IServiceProvider _serviceProvider;
    
    public GenerateEmbeddingHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var inputType = context.Node.Parameters.TryGetValue("inputType", out var type) 
            ? type?.ToString() ?? "text" 
            : "text";
        
        var modelType = context.Node.Parameters.TryGetValue("model", out var model)
            ? Enum.TryParse<EmbeddingModelType>(model?.ToString(), out var m) ? m : EmbeddingModelType.TextSmall
            : EmbeddingModelType.TextSmall;
        
        try
        {
            float[] vector;
            
            if (inputType == "image")
            {
                var imageService = _serviceProvider.GetService<IImageEmbeddingService>();
                if (imageService == null)
                {
                    return NodeExecutionResult.Fail("Image embedding servisi bulunamadı");
                }
                
                // Input'tan görüntü al
                var imageData = context.InputData switch
                {
                    byte[] bytes => bytes,
                    string base64 => Convert.FromBase64String(base64.Replace("data:image/png;base64,", "").Replace("data:image/jpeg;base64,", "")),
                    _ => null
                };
                
                if (imageData == null)
                {
                    return NodeExecutionResult.Fail("Geçerli görüntü verisi bulunamadı");
                }
                
                var result = await imageService.GenerateEmbeddingAsync(imageData, context.CancellationToken);
                vector = result.Vector;
                
                context.Log($"Görüntü embedding oluşturuldu: {result.Dimension} boyut", Shared.Enums.LogLevel.Info, null);
            }
            else
            {
                var textService = _serviceProvider.GetService<ITextEmbeddingService>();
                if (textService == null)
                {
                    return NodeExecutionResult.Fail("Text embedding servisi bulunamadı");
                }
                
                var text = context.InputData?.ToString();
                if (string.IsNullOrEmpty(text))
                {
                    return NodeExecutionResult.Fail("Metin verisi bulunamadı");
                }
                
                var result = await textService.GenerateEmbeddingAsync(text, context.CancellationToken);
                vector = result.Vector;
                
                context.Log($"Metin embedding oluşturuldu: {result.Dimension} boyut, {result.ProcessingTime.TotalMilliseconds:F0}ms", Shared.Enums.LogLevel.Info, null);
            }
            
            return NodeExecutionResult.Ok(new EmbeddingOutput
            {
                Vector = vector,
                Dimension = vector.Length,
                Model = modelType.ToString()
            });
        }
        catch (Exception ex)
        {
            return NodeExecutionResult.Fail($"Embedding hatası: {ex.Message}");
        }
    }
}

/// <summary>
/// VectorStore Node Handler
/// Embedding'i veritabanına kaydeder
/// </summary>
public class VectorStoreHandler : INodeHandler
{
    public string NodeType => "vector_store";
    
    private readonly IServiceProvider _serviceProvider;
    
    public VectorStoreHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var vectorService = _serviceProvider.GetService<IVectorDatabaseService>();
        if (vectorService == null)
        {
            return NodeExecutionResult.Fail("Vector database servisi bulunamadı");
        }
        
        // Parametreleri al
        var collection = context.Node.Parameters.TryGetValue("collection", out var col)
            ? col?.ToString() ?? "default"
            : "default";
        
        var idField = context.Node.Parameters.TryGetValue("idField", out var idF)
            ? idF?.ToString()
            : null;
        
        try
        {
            // Input'tan vektör ve metadata al
            float[]? vector = null;
            JsonDocument? metadata = null;
            string? content = null;
            string id = Guid.NewGuid().ToString();
            
            if (context.InputData is EmbeddingOutput embeddingOutput)
            {
                vector = embeddingOutput.Vector;
            }
            else if (context.InputData is Dictionary<string, object?> dict)
            {
                if (dict.TryGetValue("vector", out var v) && v is float[] vec)
                {
                    vector = vec;
                }
                else if (dict.TryGetValue("Vector", out v) && v is float[] vec2)
                {
                    vector = vec2;
                }
                
                if (dict.TryGetValue("metadata", out var m))
                {
                    metadata = JsonDocument.Parse(JsonSerializer.Serialize(m));
                }
                
                if (dict.TryGetValue("content", out var c))
                {
                    content = c?.ToString();
                }
                
                if (!string.IsNullOrEmpty(idField) && dict.TryGetValue(idField, out var idVal))
                {
                    id = idVal?.ToString() ?? id;
                }
            }
            
            if (vector == null)
            {
                return NodeExecutionResult.Fail("Vektör verisi bulunamadı");
            }
            
            // Collection var mı kontrol et, yoksa oluştur
            if (!await vectorService.CollectionExistsAsync(collection, context.CancellationToken))
            {
                await vectorService.CreateCollectionAsync(collection, vector.Length, context.CancellationToken);
                context.Log($"Collection oluşturuldu: {collection}", Shared.Enums.LogLevel.Info, null);
            }
            
            // Kaydet
            await vectorService.StoreAsync(collection, id, vector, metadata, content, context.CancellationToken);
            
            context.Log($"Vektör kaydedildi: {id} -> {collection}", Shared.Enums.LogLevel.Info, null);
            
            return NodeExecutionResult.Ok(new { id, collection, dimension = vector.Length });
        }
        catch (Exception ex)
        {
            return NodeExecutionResult.Fail($"Vector store hatası: {ex.Message}");
        }
    }
}

/// <summary>
/// VectorSearch Node Handler
/// Benzer vektörleri arar
/// </summary>
public class VectorSearchHandler : INodeHandler
{
    public string NodeType => "vector_search";
    
    private readonly IServiceProvider _serviceProvider;
    
    public VectorSearchHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var vectorService = _serviceProvider.GetService<IVectorDatabaseService>();
        if (vectorService == null)
        {
            return NodeExecutionResult.Fail("Vector database servisi bulunamadı");
        }
        
        // Parametreleri al
        var collection = context.Node.Parameters.TryGetValue("collection", out var col)
            ? col?.ToString() ?? "default"
            : "default";
        
        var topK = context.Node.Parameters.TryGetValue("topK", out var k)
            ? Convert.ToInt32(k)
            : 5;
        
        var minScore = context.Node.Parameters.TryGetValue("minScore", out var min)
            ? Convert.ToDouble(min)
            : 0.0;
        
        var metricStr = context.Node.Parameters.TryGetValue("metric", out var met)
            ? met?.ToString() ?? "cosine"
            : "cosine";
        
        var metric = metricStr.ToLowerInvariant() switch
        {
            "euclidean" => SimilarityMetric.Euclidean,
            "innerproduct" => SimilarityMetric.InnerProduct,
            _ => SimilarityMetric.Cosine
        };
        
        try
        {
            // Query vektörünü al
            float[]? queryVector = null;
            
            if (context.InputData is EmbeddingOutput embeddingOutput)
            {
                queryVector = embeddingOutput.Vector;
            }
            else if (context.InputData is float[] vec)
            {
                queryVector = vec;
            }
            else if (context.InputData is Dictionary<string, object?> dict)
            {
                if (dict.TryGetValue("vector", out var v) && v is float[] vec2)
                {
                    queryVector = vec2;
                }
            }
            
            if (queryVector == null)
            {
                return NodeExecutionResult.Fail("Query vektörü bulunamadı");
            }
            
            // Ara
            var results = await vectorService.SearchAsync(
                collection,
                queryVector,
                topK,
                minScore,
                metric,
                cancellationToken: context.CancellationToken);
            
            context.Log($"Vector search: {results.Count} sonuç bulundu (collection: {collection}, topK: {topK})", Shared.Enums.LogLevel.Info, null);
            
            // Sonuçları formatla
            var formattedResults = results.Select(r => new
            {
                r.Id,
                r.Score,
                r.Distance,
                Metadata = r.Metadata?.RootElement,
                r.Content
            }).ToList();
            
            return NodeExecutionResult.Ok(formattedResults);
        }
        catch (Exception ex)
        {
            return NodeExecutionResult.Fail($"Vector search hatası: {ex.Message}");
        }
    }
}

/// <summary>
/// VectorDelete Node Handler
/// ID'ye göre vektör siler
/// </summary>
public class VectorDeleteHandler : INodeHandler
{
    public string NodeType => "vector_delete";
    
    private readonly IServiceProvider _serviceProvider;
    
    public VectorDeleteHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var vectorService = _serviceProvider.GetService<IVectorDatabaseService>();
        if (vectorService == null)
        {
            return NodeExecutionResult.Fail("Vector database servisi bulunamadı");
        }
        
        var collection = context.Node.Parameters.TryGetValue("collection", out var col)
            ? col?.ToString() ?? "default"
            : "default";
        
        try
        {
            var id = context.InputData?.ToString();
            if (string.IsNullOrEmpty(id))
            {
                return NodeExecutionResult.Fail("Silinecek ID bulunamadı");
            }
            
            var deleted = await vectorService.DeleteAsync(collection, id, context.CancellationToken);
            
            if (deleted)
            {
                context.Log($"Vektör silindi: {id}", Shared.Enums.LogLevel.Info, null);
            }
            else
            {
                context.Log($"Vektör bulunamadı: {id}", Shared.Enums.LogLevel.Warning, null);
            }
            
            return NodeExecutionResult.Ok(new { deleted, id, collection });
        }
        catch (Exception ex)
        {
            return NodeExecutionResult.Fail($"Vector delete hatası: {ex.Message}");
        }
    }
}

/// <summary>
/// Embedding çıktısı
/// </summary>
public class EmbeddingOutput
{
    public required float[] Vector { get; init; }
    public int Dimension { get; init; }
    public string Model { get; init; } = string.Empty;
}
