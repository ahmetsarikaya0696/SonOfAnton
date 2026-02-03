using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Shared.Models;

/// <summary>
/// Haz�r workflow �ablonlar�
/// </summary>
public static class WorkflowTemplates
{
    public static List<WorkflowTemplate> GetAllTemplates() =>
    [
        // Senaryo 1: Manuel HTTP Otomasyonu
        new WorkflowTemplate
        {
            Id = "manual-http",
            Name = "Manuel HTTP �ste�i",
            Description = "Bir API'ye istek at�p sonucu i�leyen basit otomasyon",
            Category = "Temel",
            Icon = "globe",
            Color = "#89b4fa",
            Graph = new WorkflowGraphDto
            {
                Nodes =
                [
                    new WorkflowNodeDto
                    {
                        Id = Guid.Parse("00000001-0000-0000-0000-000000000001"),
                        Type = "trigger.manual",
                        Name = "Manuel Tetikleyici",
                        Icon = "play",
                        Color = "#a6e3a1",
                        X = 100,
                        Y = 200,
                        Parameters = []
                    },
                    new WorkflowNodeDto
                    {
                        Id = Guid.Parse("00000001-0000-0000-0000-000000000002"),
                        Type = "http.request",
                        Name = "HTTP �ste�i",
                        Icon = "globe",
                        Color = "#89b4fa",
                        X = 350,
                        Y = 200,
                        Parameters = new Dictionary<string, object?>
                        {
                            ["url"] = "https://jsonplaceholder.typicode.com/posts/1",
                            ["method"] = "GET"
                        }
                    },
                    new WorkflowNodeDto
                    {
                        Id = Guid.Parse("00000001-0000-0000-0000-000000000003"),
                        Type = "transform.set",
                        Name = "Veri D�n���m�",
                        Icon = "edit",
                        Color = "#f9e2af",
                        X = 600,
                        Y = 200,
                        Parameters = new Dictionary<string, object?>
                        {
                            ["expression"] = "{ \"baslik\": data.title, \"ozet\": data.body }"
                        }
                    },
                    new WorkflowNodeDto
                    {
                        Id = Guid.Parse("00000001-0000-0000-0000-000000000004"),
                        Type = "debug",
                        Name = "Debug",
                        Icon = "terminal",
                        Color = "#cba6f7",
                        X = 850,
                        Y = 200,
                        Parameters = []
                    }
                ],
                Connections =
                [
                    new NodeConnectionDto
                    {
                        Id = Guid.NewGuid(),
                        SourceNodeId = Guid.Parse("00000001-0000-0000-0000-000000000001"),
                        TargetNodeId = Guid.Parse("00000001-0000-0000-0000-000000000002"),
                        SourcePort = "output",
                        TargetPort = "input"
                    },
                    new NodeConnectionDto
                    {
                        Id = Guid.NewGuid(),
                        SourceNodeId = Guid.Parse("00000001-0000-0000-0000-000000000002"),
                        TargetNodeId = Guid.Parse("00000001-0000-0000-0000-000000000003"),
                        SourcePort = "output",
                        TargetPort = "input"
                    },
                    new NodeConnectionDto
                    {
                        Id = Guid.NewGuid(),
                        SourceNodeId = Guid.Parse("00000001-0000-0000-0000-000000000003"),
                        TargetNodeId = Guid.Parse("00000001-0000-0000-0000-000000000004"),
                        SourcePort = "output",
                        TargetPort = "input"
                    }
                ]
            }
        },

        // Senaryo 2: Vector Search Otomasyonu
        new WorkflowTemplate
        {
            Id = "vector-search",
            Name = "Semantik Arama (Vector Search)",
            Description = "Metni embedding'e �evirip veritaban�nda benzer i�erikleri bulan otomasyon",
            Category = "Yapay Zeka",
            Icon = "search",
            Color = "#74c7ec",
            Graph = new WorkflowGraphDto
            {
                Nodes =
                [
                    new WorkflowNodeDto
                    {
                        Id = Guid.Parse("00000002-0000-0000-0000-000000000001"),
                        Type = "trigger.manual",
                        Name = "Manuel Tetikleyici",
                        Icon = "play",
                        Color = "#a6e3a1",
                        X = 100,
                        Y = 200,
                        Parameters = []
                    },
                    new WorkflowNodeDto
                    {
                        Id = Guid.Parse("00000002-0000-0000-0000-000000000002"),
                        Type = "transform.set",
                        Name = "Sorgu Ayarla",
                        Icon = "edit",
                        Color = "#f9e2af",
                        X = 350,
                        Y = 200,
                        Parameters = new Dictionary<string, object?>
                        {
                            ["expression"] = "{ \"query\": \"depresyon belirtileri nelerdir?\" }"
                        }
                    },
                    new WorkflowNodeDto
                    {
                        Id = Guid.Parse("00000002-0000-0000-0000-000000000003"),
                        Type = "ai.embed",
                        Name = "Embedder",
                        Icon = "layers",
                        Color = "#74c7ec",
                        X = 600,
                        Y = 200,
                        Parameters = new Dictionary<string, object?>
                        {
                            ["model"] = "nomic-embed-text",
                            ["input"] = "{{query}}"
                        }
                    },
                    new WorkflowNodeDto
                    {
                        Id = Guid.Parse("00000002-0000-0000-0000-000000000004"),
                        Type = "ai.search",
                        Name = "Vector Search",
                        Icon = "search",
                        Color = "#74c7ec",
                        X = 850,
                        Y = 200,
                        Parameters = new Dictionary<string, object?>
                        {
                            ["collection"] = "documents",
                            ["topK"] = 5
                        }
                    },
                    new WorkflowNodeDto
                    {
                        Id = Guid.Parse("00000002-0000-0000-0000-000000000005"),
                        Type = "debug",
                        Name = "Debug",
                        Icon = "terminal",
                        Color = "#cba6f7",
                        X = 1100,
                        Y = 200,
                        Parameters = []
                    }
                ],
                Connections =
                [
                    new NodeConnectionDto
                    {
                        Id = Guid.NewGuid(),
                        SourceNodeId = Guid.Parse("00000002-0000-0000-0000-000000000001"),
                        TargetNodeId = Guid.Parse("00000002-0000-0000-0000-000000000002"),
                        SourcePort = "output",
                        TargetPort = "input"
                    },
                    new NodeConnectionDto
                    {
                        Id = Guid.NewGuid(),
                        SourceNodeId = Guid.Parse("00000002-0000-0000-0000-000000000002"),
                        TargetNodeId = Guid.Parse("00000002-0000-0000-0000-000000000003"),
                        SourcePort = "output",
                        TargetPort = "input"
                    },
                    new NodeConnectionDto
                    {
                        Id = Guid.NewGuid(),
                        SourceNodeId = Guid.Parse("00000002-0000-0000-0000-000000000003"),
                        TargetNodeId = Guid.Parse("00000002-0000-0000-0000-000000000004"),
                        SourcePort = "output",
                        TargetPort = "input"
                    },
                    new NodeConnectionDto
                    {
                        Id = Guid.NewGuid(),
                        SourceNodeId = Guid.Parse("00000002-0000-0000-0000-000000000004"),
                        TargetNodeId = Guid.Parse("00000002-0000-0000-0000-000000000005"),
                        SourcePort = "output",
                        TargetPort = "input"
                    }
                ]
            }
        },

        // Bonus: LLM ile Soru-Cevap
        new WorkflowTemplate
        {
            Id = "llm-qa",
            Name = "LLM Soru-Cevap",
            Description = "Yapay zeka ile soru-cevap otomasyonu",
            Category = "Yapay Zeka",
            Icon = "cpu",
            Color = "#74c7ec",
            Graph = new WorkflowGraphDto
            {
                Nodes =
                [
                    new WorkflowNodeDto
                    {
                        Id = Guid.Parse("00000003-0000-0000-0000-000000000001"),
                        Type = "trigger.manual",
                        Name = "Manuel Tetikleyici",
                        Icon = "play",
                        Color = "#a6e3a1",
                        X = 100,
                        Y = 200,
                        Parameters = []
                    },
                    new WorkflowNodeDto
                    {
                        Id = Guid.Parse("00000003-0000-0000-0000-000000000002"),
                        Type = "transform.set",
                        Name = "Soru Ayarla",
                        Icon = "edit",
                        Color = "#f9e2af",
                        X = 350,
                        Y = 200,
                        Parameters = new Dictionary<string, object?>
                        {
                            ["expression"] = "{ \"question\": \"Anksiyete nedir ve nasil ba� edilir?\" }"
                        }
                    },
                    new WorkflowNodeDto
                    {
                        Id = Guid.Parse("00000003-0000-0000-0000-000000000003"),
                        Type = "ai.llm",
                        Name = "LLM",
                        Icon = "cpu",
                        Color = "#74c7ec",
                        X = 600,
                        Y = 200,
                        Parameters = new Dictionary<string, object?>
                        {
                            ["prompt"] = "Sen bir psikoloji uzman�s�n. �u soruyu yan�tla: {{question}}",
                            ["model"] = "qwen2.5-coder:14b"
                        }
                    },
                    new WorkflowNodeDto
                    {
                        Id = Guid.Parse("00000003-0000-0000-0000-000000000004"),
                        Type = "debug",
                        Name = "Debug",
                        Icon = "terminal",
                        Color = "#cba6f7",
                        X = 850,
                        Y = 200,
                        Parameters = []
                    }
                ],
                Connections =
                [
                    new NodeConnectionDto
                    {
                        Id = Guid.NewGuid(),
                        SourceNodeId = Guid.Parse("00000003-0000-0000-0000-000000000001"),
                        TargetNodeId = Guid.Parse("00000003-0000-0000-0000-000000000002"),
                        SourcePort = "output",
                        TargetPort = "input"
                    },
                    new NodeConnectionDto
                    {
                        Id = Guid.NewGuid(),
                        SourceNodeId = Guid.Parse("00000003-0000-0000-0000-000000000002"),
                        TargetNodeId = Guid.Parse("00000003-0000-0000-0000-000000000003"),
                        SourcePort = "output",
                        TargetPort = "input"
                    },
                    new NodeConnectionDto
                    {
                        Id = Guid.NewGuid(),
                        SourceNodeId = Guid.Parse("00000003-0000-0000-0000-000000000003"),
                        TargetNodeId = Guid.Parse("00000003-0000-0000-0000-000000000004"),
                        SourcePort = "output",
                        TargetPort = "input"
                    }
                ]
            }
        }
    ];

    public static WorkflowTemplate? GetTemplateById(string id) =>
        GetAllTemplates().FirstOrDefault(t => t.Id == id);

    public static Dictionary<string, List<WorkflowTemplate>> GetTemplatesByCategory() =>
        GetAllTemplates()
            .GroupBy(t => t.Category)
            .ToDictionary(g => g.Key, g => g.ToList());
}

/// <summary>
/// Workflow �ablon modeli
/// </summary>
public class WorkflowTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public WorkflowGraphDto Graph { get; set; } = new();
}

