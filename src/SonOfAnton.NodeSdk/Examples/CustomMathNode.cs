using SonOfAnton.NodeSdk;

namespace SonOfAnton.NodeSdk.Examples;

/// <summary>
/// Örnek SDK node — iki sayıyı toplar.
/// Bu sınıf, SDK kullanımını göstermek için hazırlanmıştır.
/// </summary>
[NodeDefinition("math.add", "Toplama", Category = "Matematik", Icon = "plus", Color = "#94e2d5",
    Description = "İki sayıyı toplar")]
[NodeInput("a", "A Değeri", DataType = "number")]
[NodeInput("b", "B Değeri", DataType = "number")]
[NodeOutput("result", "Sonuç", DataType = "number")]
[NodeParameter("precision", "Hassasiyet (ondalık)", ParamType = "number", DefaultValue = 2)]
public class CustomMathNode : ICustomNodeExecutor
{
    public Task<Dictionary<string, object?>> ExecuteAsync(
        Dictionary<string, object?> inputs,
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        var a = Convert.ToDouble(inputs.GetValueOrDefault("a", 0));
        var b = Convert.ToDouble(inputs.GetValueOrDefault("b", 0));
        var precision = Convert.ToInt32(parameters.GetValueOrDefault("precision", 2));

        var result = Math.Round(a + b, precision);

        return Task.FromResult(new Dictionary<string, object?>
        {
            ["result"] = result
        });
    }
}
