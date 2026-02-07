namespace SonOfAnton.Shared.Models;

/// <summary>
/// Tum kullanilabilir node tiplerini tanimlar
/// </summary>
public static class NodeRegistry
{
    public static List<NodeDefinition> GetAllNodes() =>
    [
        // Tetikleyiciler
        new NodeDefinition
        {
            Type = "trigger.manual",
            Name = "Manuel Tetikleyici",
            Icon = "play",
            Category = "Tetikleyiciler",
            Description = "Is akisini manuel baslat",
            Color = "#a6e3a1",
            Outputs = [new NodePort { Name = "output", Type = "any" }]
        },
        new NodeDefinition
        {
            Type = "trigger.schedule",
            Name = "Zamanli Tetikleyici",
            Icon = "clock",
            Category = "Tetikleyiciler",
            Description = "Belirli zamanlarda calistir",
            Color = "#a6e3a1",
            Outputs = [new NodePort { Name = "output", Type = "any" }],
            Parameters =
            [
                new NodeParameter
                {
                    Name = "cron",
                    DisplayName = "Cron Ifadesi",
                    Type = "string",
                    Placeholder = "0 * * * *",
                    Required = true
                }
            ]
        },
        new NodeDefinition
        {
            Type = "trigger.webhook",
            Name = "Webhook",
            Icon = "webhook",
            Category = "Tetikleyiciler",
            Description = "HTTP webhook ile tetikle",
            Color = "#a6e3a1",
            Outputs = [new NodePort { Name = "output", Type = "any" }],
            Parameters =
            [
                new NodeParameter
                {
                    Name = "method",
                    DisplayName = "HTTP Metodu",
                    Type = "select",
                    DefaultValue = "POST",
                    Options =
                    [
                        new SelectOption { Value = "GET", Label = "GET" },
                        new SelectOption { Value = "POST", Label = "POST" },
                        new SelectOption { Value = "PUT", Label = "PUT" },
                        new SelectOption { Value = "DELETE", Label = "DELETE" }
                    ]
                }
            ]
        },

        // HTTP
        new NodeDefinition
        {
            Type = "http.request",
            Name = "HTTP Istegi",
            Icon = "globe",
            Category = "HTTP",
            Description = "HTTP istekleri gonder",
            Color = "#89b4fa",
            Inputs = [new NodePort { Name = "input", Type = "any" }],
            Outputs = [new NodePort { Name = "output", Type = "any" }],
            Parameters =
            [
                new NodeParameter { Name = "url", DisplayName = "URL", Type = "string", Required = true, Placeholder = "https://api.example.com" },
                new NodeParameter
                {
                    Name = "method",
                    DisplayName = "Metod",
                    Type = "select",
                    DefaultValue = "GET",
                    Options =
                    [
                        new SelectOption { Value = "GET", Label = "GET" },
                        new SelectOption { Value = "POST", Label = "POST" },
                        new SelectOption { Value = "PUT", Label = "PUT" },
                        new SelectOption { Value = "DELETE", Label = "DELETE" },
                        new SelectOption { Value = "PATCH", Label = "PATCH" }
                    ]
                },
                new NodeParameter { Name = "headers", DisplayName = "Basliklar", Type = "json", DefaultValue = "{}" },
                new NodeParameter { Name = "body", DisplayName = "Govde", Type = "json", DefaultValue = "{}" }
            ]
        },

        // Veri Donusumu
        new NodeDefinition
        {
            Type = "transform.set",
            Name = "Deger Ata",
            Icon = "edit",
            Category = "Donusum",
            Description = "Degerleri ayarla",
            Color = "#f9e2af",
            Inputs = [new NodePort { Name = "input", Type = "any" }],
            Outputs = [new NodePort { Name = "output", Type = "any" }],
            Parameters =
            [
                new NodeParameter { Name = "values", DisplayName = "Degerler", Type = "json", Required = true }
            ]
        },
        new NodeDefinition
        {
            Type = "transform.code",
            Name = "Kod",
            Icon = "code",
            Category = "Donusum",
            Description = "Ozel JavaScript kodu calistir",
            Color = "#f9e2af",
            Inputs = [new NodePort { Name = "input", Type = "any" }],
            Outputs = [new NodePort { Name = "output", Type = "any" }],
            Parameters =
            [
                new NodeParameter { Name = "code", DisplayName = "JavaScript Kodu", Type = "code", Required = true, DefaultValue = "return items;" }
            ]
        },
        new NodeDefinition
        {
            Type = "transform.filter",
            Name = "Filtre",
            Icon = "filter",
            Category = "Donusum",
            Description = "Kosullara gore filtrele",
            Color = "#f9e2af",
            Inputs = [new NodePort { Name = "input", Type = "any" }],
            Outputs = [new NodePort { Name = "output", Type = "any" }],
            Parameters =
            [
                new NodeParameter { Name = "condition", DisplayName = "Kosul", Type = "string", Required = true, Placeholder = "item.value > 10" }
            ]
        },
        new NodeDefinition
        {
            Type = "transform.template",
            Name = "Sablon",
            Icon = "file-text",
            Category = "Donusum",
            Description = "Mustache sablonu ile veri formatla",
            Color = "#f9e2af",
            Inputs = [new NodePort { Name = "input", Type = "any" }],
            Outputs = [new NodePort { Name = "output", Type = "any" }],
            Parameters =
            [
                new NodeParameter { Name = "template", DisplayName = "Sablon", Type = "string", Required = true, Placeholder = "Merhaba {{name}}, siparisiz #{{orderId}} hazirlandi." }
            ]
        },
        new NodeDefinition
        {
            Type = "transform.aggregate",
            Name = "Toplama",
            Icon = "bar-chart-2",
            Category = "Donusum",
            Description = "Array verilerini topla (count, sum, avg, min, max)",
            Color = "#f9e2af",
            Inputs = [new NodePort { Name = "input", Type = "any" }],
            Outputs = [new NodePort { Name = "output", Type = "any" }],
            Parameters =
            [
                new NodeParameter
                {
                    Name = "operation",
                    DisplayName = "Islem",
                    Type = "select",
                    DefaultValue = "count",
                    Options =
                    [
                        new SelectOption { Value = "count", Label = "Say" },
                        new SelectOption { Value = "sum", Label = "Topla" },
                        new SelectOption { Value = "avg", Label = "Ortalama" },
                        new SelectOption { Value = "min", Label = "Minimum" },
                        new SelectOption { Value = "max", Label = "Maksimum" },
                        new SelectOption { Value = "first", Label = "Ilk" },
                        new SelectOption { Value = "last", Label = "Son" },
                        new SelectOption { Value = "distinct", Label = "Benzersiz" }
                    ]
                },
                new NodeParameter { Name = "field", DisplayName = "Alan (opsiyonel)", Type = "string", Placeholder = "price" }
            ]
        },

        // Mantik
        new NodeDefinition
        {
            Type = "logic.if",
            Name = "Kosul (IF)",
            Icon = "git-branch",
            Category = "Mantik",
            Description = "Kosullu dallanma",
            Color = "#cba6f7",
            Inputs = [new NodePort { Name = "input", Type = "any" }],
            Outputs =
            [
                new NodePort { Name = "true", Type = "any" },
                new NodePort { Name = "false", Type = "any" }
            ],
            Parameters =
            [
                new NodeParameter { Name = "condition", DisplayName = "Kosul", Type = "string", Required = true }
            ]
        },
        new NodeDefinition
        {
            Type = "logic.switch",
            Name = "Yonlendirici",
            Icon = "shuffle",
            Category = "Mantik",
            Description = "Farkli cikislara yonlendir",
            Color = "#cba6f7",
            Inputs = [new NodePort { Name = "input", Type = "any" }],
            Outputs =
            [
                new NodePort { Name = "output0", Type = "any" },
                new NodePort { Name = "output1", Type = "any" },
                new NodePort { Name = "output2", Type = "any" }
            ],
            Parameters =
            [
                new NodeParameter { Name = "value", DisplayName = "Kontrol Edilecek Deger", Type = "string", Required = true }
            ]
        },
        new NodeDefinition
        {
            Type = "logic.merge",
            Name = "Birlestir",
            Icon = "git-merge",
            Category = "Mantik",
            Description = "Birden fazla girisi birlestir",
            Color = "#cba6f7",
            Inputs =
            [
                new NodePort { Name = "input1", Type = "any" },
                new NodePort { Name = "input2", Type = "any" }
            ],
            Outputs = [new NodePort { Name = "output", Type = "any" }]
        },
        new NodeDefinition
        {
            Type = "logic.delay",
            Name = "Bekle",
            Icon = "clock",
            Category = "Mantik",
            Description = "Belirtilen sure kadar bekle",
            Color = "#cba6f7",
            Inputs = [new NodePort { Name = "input", Type = "any" }],
            Outputs = [new NodePort { Name = "output", Type = "any" }],
            Parameters =
            [
                new NodeParameter { Name = "delay", DisplayName = "Bekleme Suresi (ms)", Type = "number", DefaultValue = 1000, Required = true }
            ]
        },
        new NodeDefinition
        {
            Type = "logic.loop",
            Name = "Dongu",
            Icon = "repeat",
            Category = "Mantik",
            Description = "Array uzerinde iterasyon yap",
            Color = "#cba6f7",
            Inputs = [new NodePort { Name = "input", Type = "any" }],
            Outputs = [new NodePort { Name = "output", Type = "any" }]
        },

        // Yapay Zeka
        new NodeDefinition
        {
            Type = "ai.llm",
            Name = "LLM",
            Icon = "cpu",
            Category = "Yapay Zeka",
            Description = "Buyuk Dil Modeli ile metin uret",
            Color = "#74c7ec",
            Inputs = [new NodePort { Name = "input", Type = "any" }],
            Outputs = [new NodePort { Name = "output", Type = "any" }],
            Parameters =
            [
                new NodeParameter { Name = "prompt", DisplayName = "Prompt", Type = "string", Required = true },
                new NodeParameter { Name = "systemPrompt", DisplayName = "Sistem Promptu", Type = "string" }
            ]
        },
        new NodeDefinition
        {
            Type = "ai.openai",
            Name = "OpenAI",
            Icon = "cpu",
            Category = "Yapay Zeka",
            Description = "OpenAI API cagir",
            Color = "#74c7ec",
            Inputs = [new NodePort { Name = "input", Type = "any" }],
            Outputs = [new NodePort { Name = "output", Type = "any" }],
            Parameters =
            [
                new NodeParameter
                {
                    Name = "model",
                    DisplayName = "Model",
                    Type = "select",
                    DefaultValue = "gpt-4o",
                    Options =
                    [
                        new SelectOption { Value = "gpt-4o", Label = "GPT-4o" },
                        new SelectOption { Value = "gpt-4o-mini", Label = "GPT-4o Mini" },
                        new SelectOption { Value = "gpt-4-turbo", Label = "GPT-4 Turbo" }
                    ]
                },
                new NodeParameter { Name = "prompt", DisplayName = "Prompt", Type = "string", Required = true }
            ]
        },
        new NodeDefinition
        {
            Type = "ai.embed",
            Name = "Embedding",
            Icon = "layers",
            Category = "Yapay Zeka",
            Description = "Metni vektore cevir ve kaydet",
            Color = "#74c7ec",
            Inputs = [new NodePort { Name = "input", Type = "any" }],
            Outputs = [new NodePort { Name = "output", Type = "any" }],
            Parameters =
            [
                new NodeParameter { Name = "text", DisplayName = "Metin", Type = "string" },
                new NodeParameter { Name = "collection", DisplayName = "Koleksiyon", Type = "string", DefaultValue = "default" }
            ]
        },
        new NodeDefinition
        {
            Type = "ai.search",
            Name = "Vektor Arama",
            Icon = "search",
            Category = "Yapay Zeka",
            Description = "Benzer metinleri bul (Cosine Similarity)",
            Color = "#74c7ec",
            Inputs = [new NodePort { Name = "input", Type = "any" }],
            Outputs = [new NodePort { Name = "output", Type = "any" }],
            Parameters =
            [
                new NodeParameter { Name = "query", DisplayName = "Sorgu", Type = "string" },
                new NodeParameter { Name = "collection", DisplayName = "Koleksiyon", Type = "string", DefaultValue = "default" },
                new NodeParameter { Name = "topK", DisplayName = "Sonuc Sayisi", Type = "number", DefaultValue = 5 }
            ]
        },

        // Veri
        new NodeDefinition
        {
            Type = "data.database",
            Name = "Veritabani",
            Icon = "database",
            Category = "Veri",
            Description = "Veritabani sorgulari calistir",
            Color = "#fab387",
            Inputs = [new NodePort { Name = "input", Type = "any" }],
            Outputs = [new NodePort { Name = "output", Type = "any" }],
            Parameters =
            [
                new NodeParameter { Name = "operation", DisplayName = "Islem", Type = "select", DefaultValue = "select",
                    Options =
                    [
                        new SelectOption { Value = "select", Label = "Sec" },
                        new SelectOption { Value = "insert", Label = "Ekle" },
                        new SelectOption { Value = "update", Label = "Guncelle" },
                        new SelectOption { Value = "delete", Label = "Sil" }
                    ]
                },
                new NodeParameter { Name = "query", DisplayName = "Sorgu", Type = "code", Required = true }
            ]
        },

        // Iletisim
        new NodeDefinition
        {
            Type = "comm.email",
            Name = "E-posta Gonder",
            Icon = "mail",
            Category = "Iletisim",
            Description = "E-posta gonder",
            Color = "#eba0ac",
            Inputs = [new NodePort { Name = "input", Type = "any" }],
            Outputs = [new NodePort { Name = "output", Type = "any" }],
            Parameters =
            [
                new NodeParameter { Name = "to", DisplayName = "Alici", Type = "string", Required = true },
                new NodeParameter { Name = "subject", DisplayName = "Konu", Type = "string", Required = true },
                new NodeParameter { Name = "body", DisplayName = "Icerik", Type = "string" }
            ]
        },
        new NodeDefinition
        {
            Type = "comm.slack",
            Name = "Slack",
            Icon = "message-square",
            Category = "Iletisim",
            Description = "Slack mesaji gonder",
            Color = "#eba0ac",
            Inputs = [new NodePort { Name = "input", Type = "any" }],
            Outputs = [new NodePort { Name = "output", Type = "any" }],
            Parameters =
            [
                new NodeParameter { Name = "channel", DisplayName = "Kanal", Type = "string", Required = true },
                new NodeParameter { Name = "message", DisplayName = "Mesaj", Type = "string", Required = true }
            ]
        },
        
        // Vector DB
        new NodeDefinition
        {
            Type = "generate_embedding",
            Name = "Embedding Olustur",
            Icon = "layers",
            Category = "Vector DB",
            Description = "Metin veya goruntuyu vektore donustur",
            Color = "#94e2d5",
            Inputs = [new NodePort { Name = "input", Type = "any" }],
            Outputs = [new NodePort { Name = "output", Type = "vector" }],
            Parameters =
            [
                new NodeParameter
                {
                    Name = "inputType",
                    DisplayName = "Giris Tipi",
                    Type = "select",
                    DefaultValue = "text",
                    Options =
                    [
                        new SelectOption { Value = "text", Label = "Metin" },
                        new SelectOption { Value = "image", Label = "Goruntu" }
                    ]
                },
                new NodeParameter
                {
                    Name = "model",
                    DisplayName = "Model",
                    Type = "select",
                    DefaultValue = "TextSmall",
                    Options =
                    [
                        new SelectOption { Value = "TextSmall", Label = "MiniLM (384d, Hizli)" },
                        new SelectOption { Value = "TextMedium", Label = "BGE-Small (384d)" },
                        new SelectOption { Value = "ClipVision", Label = "CLIP Vision (512d)" }
                    ]
                }
            ]
        },
        new NodeDefinition
        {
            Type = "vector_store",
            Name = "Vektor Kaydet",
            Icon = "database",
            Category = "Vector DB",
            Description = "Embedding vektorunu PostgreSQL'e kaydet",
            Color = "#94e2d5",
            Inputs = [new NodePort { Name = "input", Type = "vector" }],
            Outputs = [new NodePort { Name = "output", Type = "any" }],
            Parameters =
            [
                new NodeParameter { Name = "collection", DisplayName = "Koleksiyon", Type = "string", DefaultValue = "default", Required = true },
                new NodeParameter { Name = "idField", DisplayName = "ID Alani (opsiyonel)", Type = "string" }
            ]
        },
        new NodeDefinition
        {
            Type = "vector_search",
            Name = "Vektor Ara",
            Icon = "search",
            Category = "Vector DB",
            Description = "Benzer vektorleri bul (Cosine/Euclidean)",
            Color = "#94e2d5",
            Inputs = [new NodePort { Name = "input", Type = "vector" }],
            Outputs = [new NodePort { Name = "output", Type = "any" }],
            Parameters =
            [
                new NodeParameter { Name = "collection", DisplayName = "Koleksiyon", Type = "string", DefaultValue = "default", Required = true },
                new NodeParameter { Name = "topK", DisplayName = "Sonuc Sayisi", Type = "number", DefaultValue = 5 },
                new NodeParameter { Name = "minScore", DisplayName = "Min Skor (0-1)", Type = "number", DefaultValue = 0.0 },
                new NodeParameter
                {
                    Name = "metric",
                    DisplayName = "Metrik",
                    Type = "select",
                    DefaultValue = "cosine",
                    Options =
                    [
                        new SelectOption { Value = "cosine", Label = "Cosine Similarity" },
                        new SelectOption { Value = "euclidean", Label = "Euclidean Distance" },
                        new SelectOption { Value = "innerproduct", Label = "Inner Product" }
                    ]
                }
            ]
        },
        new NodeDefinition
        {
            Type = "vector_delete",
            Name = "Vektor Sil",
            Icon = "trash-2",
            Category = "Vector DB",
            Description = "ID'ye gore vektor sil",
            Color = "#94e2d5",
            Inputs = [new NodePort { Name = "input", Type = "any" }],
            Outputs = [new NodePort { Name = "output", Type = "any" }],
            Parameters =
            [
                new NodeParameter { Name = "collection", DisplayName = "Koleksiyon", Type = "string", DefaultValue = "default", Required = true }
            ]
        }
    ];

    public static Dictionary<string, List<NodeDefinition>> GetNodesByCategory()
    {
        return GetAllNodes()
            .GroupBy(n => n.Category)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    public static NodeDefinition? GetNode(string type) =>
        GetAllNodes().FirstOrDefault(n => n.Type == type);
}

