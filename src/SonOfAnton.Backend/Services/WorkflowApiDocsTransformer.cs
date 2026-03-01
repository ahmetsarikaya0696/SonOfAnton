using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using SonOfAnton.Backend.Data;
using System.Text.Json;
using System.Net.Http;

namespace SonOfAnton.Backend.Services;

/// <summary>
/// Veritabanındaki aktif Workflow API Endpoint'lerini OpenAPI (Swagger) dokümantasyonuna ekler.
/// </summary>
public class WorkflowApiDocsTransformer : IOpenApiDocumentTransformer
{
    private readonly IServiceProvider _serviceProvider;

    public WorkflowApiDocsTransformer(IServiceProvider serviceProvider)
    {
        // Document transformer singleton olduğu için scoped servisleri IServiceProvider'dan alıyoruz
        _serviceProvider = serviceProvider;
    }

    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();

        var endpoints = await dbContext.WorkflowApiEndpoints
            .Include(e => e.Workflow)
            .Where(e => e.IsActive)
            .ToListAsync(cancellationToken);

        if (!endpoints.Any()) return;

        // Eğer eksikse ApiKey şema tanımını ekle
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        if (!document.Components.SecuritySchemes.ContainsKey("ApiKey"))
        {
            var apiKeyScheme = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = "X-Api-Key",
                Description = "Workflow API Endpoint'leri ve admin API'leri için yetkilendirme anahtarı."
            };
            document.Components.SecuritySchemes.Add("ApiKey", apiKeyScheme);
        }

        /*
        // ApiKey gereksinimi için model tanımı (şu anki sürümde tip uyuşmazlığı nedeniyle devre dışı)
        var apiKeySecurityRequirement = new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" } }] = new List<string>()
        };
        */

        foreach (var endpoint in endpoints)
        {
            var path = $"/api/published/{endpoint.Route.TrimStart('/')}";

            if (!document.Paths.TryGetValue(path, out var pathItem))
            {
                pathItem = new OpenApiPathItem();
                document.Paths.Add(path, pathItem);
            }

            var httpMethodStr = endpoint.HttpMethod.ToUpper();
            var httpMethod = new HttpMethod(httpMethodStr);
            
            var operation = new OpenApiOperation
            {
                Summary = $"Workflow: {endpoint.Workflow?.Name}",
                Description = endpoint.Description ?? "Dinamik olarak yayınlanmış workflow tetikleme endpoint'i.",
                Responses = new OpenApiResponses()
            };

            // Kimlik doğrulama ayarları
            if (endpoint.RequireApiKey)
            {
                operation.Security ??= new List<OpenApiSecurityRequirement>();
                // operation.Security.Add(apiKeySecurityRequirement);
            }

            // InputSchema'ya göre request body ayarları
            if (endpoint.InputSchema != null)
            {
                var schemaDoc = endpoint.InputSchema;
                var schemaProperties = new Dictionary<string, IOpenApiSchema>();

                if (schemaDoc != null && schemaDoc.RootElement.ValueKind == JsonValueKind.Object && schemaDoc.RootElement.TryGetProperty("properties", out var props))
                {
                    foreach (var prop in props.EnumerateObject())
                    {
                        var propType = prop.Value.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : "string";
                        var propDesc = prop.Value.TryGetProperty("description", out var descEl) ? descEl.GetString() : null;

                        var schemaType = propType?.ToLowerInvariant() switch
                        {
                            "integer" => JsonSchemaType.Integer,
                            "number" => JsonSchemaType.Number,
                            "boolean" => JsonSchemaType.Boolean,
                            "array" => JsonSchemaType.Array,
                            "object" => JsonSchemaType.Object,
                            _ => JsonSchemaType.String
                        };

                        schemaProperties.Add(prop.Name, new OpenApiSchema
                        {
                            Type = schemaType,
                            Description = propDesc
                        });
                    }
                }

                operation.RequestBody = new OpenApiRequestBody
                {
                    Required = true,
                    Description = "Workflow giriş parametreleri",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Schema = new OpenApiSchema
                            {
                                Type = JsonSchemaType.Object,
                                Properties = schemaProperties
                            }
                        }
                    }
                };
            }
            else if (httpMethodStr == "POST" || httpMethodStr == "PUT")
            {
                operation.RequestBody = new OpenApiRequestBody
                {
                    Required = false,
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType { Schema = new OpenApiSchema { Type = JsonSchemaType.Object } }
                    }
                };
            }

            // Yanıt (Response) ayarları
            var successCode = endpoint.WaitForResult ? "200" : "202";
            var successDescription = endpoint.WaitForResult ? "Workflow başarıyla çalıştı ve sonuç döndü." : "Workflow arka planda çalışmak üzere kabul edildi.";

            operation.Responses.Add(successCode, new OpenApiResponse
            {
                Description = successDescription,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [endpoint.ResponseContentType ?? "application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema { Type = JsonSchemaType.Object }
                    }
                }
            });

            if (endpoint.RequireApiKey)
                operation.Responses.Add("401", new OpenApiResponse { Description = "Unauthorized - Missing or invalid API key" });

            if (endpoint.WaitForResult)
                operation.Responses.Add("504", new OpenApiResponse { Description = "Gateway Timeout - Workflow execution took too long" });

            pathItem.Operations!.Add(httpMethod, operation);
        }
    }
}
