using SonOfAnton.NodeSdk;

namespace SonOfAnton.CustomNode;

/// <summary>
/// Özel node implementasyonu.
/// Bu sınıfı kendi ihtiyaçlarınıza göre düzenleyin.
///
/// Kullanım:
///   1. Attribute'ları düzenleyerek node tipini, adını, giriş/çıkış pinlerini ve parametrelerini tanımlayın.
///   2. ExecuteAsync metodunda iş mantığınızı yazın.
///   3. Projeyi derleyip oluşan DLL'i SonOfAnton Backend'inin plugins/ dizinine kopyalayın.
///   4. Backend'i yeniden başlattığınızda node otomatik olarak palette görünecektir.
/// </summary>
[NodeDefinition("custom.mynode", "Özel Node", Category = "Özel", Icon = "puzzle", Color = "#89dceb",
    Description = "Açıklama buraya yazılır")]
[NodeInput("input", "Giriş", DataType = "any")]
[NodeOutput("output", "Çıkış", DataType = "any")]
[NodeParameter("setting", "Ayar", ParamType = "string", DefaultValue = "varsayılan")]
public class MyCustomNode : ICustomNodeExecutor
{
    public Task<Dictionary<string, object?>> ExecuteAsync(
        Dictionary<string, object?> inputs,
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        // Giriş verisini al
        var input = inputs.GetValueOrDefault("input");
        var setting = parameters.GetValueOrDefault("setting")?.ToString() ?? "";

        // TODO: İş mantığınızı buraya yazın
        var result = new
        {
            processedInput = input,
            appliedSetting = setting,
            timestamp = DateTime.UtcNow
        };

        return Task.FromResult(new Dictionary<string, object?>
        {
            ["output"] = result
        });
    }
}
