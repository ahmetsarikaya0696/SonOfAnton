namespace SonOfAnton.NodeSdk;

/// <summary>
/// Custom node çalıştırma arayüzü.
/// Plugin olarak geliştirilen her node bu arayüzü implement etmelidir.
/// </summary>
/// <example>
/// [NodeDefinition("math.add", "Toplama")]
/// [NodeInput("a", "A Değeri", DataType = "number")]
/// [NodeInput("b", "B Değeri", DataType = "number")]
/// [NodeOutput("result", "Sonuç", DataType = "number")]
/// public class AddNode : ICustomNodeExecutor
/// {
///     public Task&lt;Dictionary&lt;string, object?&gt;&gt; ExecuteAsync(
///         Dictionary&lt;string, object?&gt; inputs,
///         Dictionary&lt;string, object?&gt; parameters,
///         CancellationToken ct)
///     {
///         var a = Convert.ToDouble(inputs.GetValueOrDefault("a", 0));
///         var b = Convert.ToDouble(inputs.GetValueOrDefault("b", 0));
///         return Task.FromResult(new Dictionary&lt;string, object?&gt; { ["result"] = a + b });
///     }
/// }
/// </example>
public interface ICustomNodeExecutor
{
    /// <summary>
    /// Node'u çalıştırır.
    /// </summary>
    /// <param name="inputs">Giriş pin'lerinden gelen değerler (pin adı → değer).</param>
    /// <param name="parameters">UI'dan düzenlenen parametre değerleri.</param>
    /// <param name="cancellationToken">İptal token'ı.</param>
    /// <returns>Çıkış pin'lerine yazılacak değerler (pin adı → değer).</returns>
    Task<Dictionary<string, object?>> ExecuteAsync(
        Dictionary<string, object?> inputs,
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken = default);
}
