using System.Reflection;
using SonOfAnton.Shared.Models;

namespace SonOfAnton.Backend.Services;

/// <summary>
/// Plugin assembly'lerinden node tanımlarını yükleyen servis.
/// Plugins dizinindeki .dll dosyalarını tarar ve NodeSdk attribute'ları ile işaretlenmiş sınıfları bulur.
/// </summary>
public class PluginLoaderService
{
    private readonly ILogger<PluginLoaderService> _logger;
    private readonly List<NodeDefinition> _pluginNodes = [];
    private readonly string _pluginsPath;

    public PluginLoaderService(ILogger<PluginLoaderService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _pluginsPath = configuration.GetValue<string>("Plugins:Path")
            ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
    }

    /// <summary>
    /// Plugin dizinini tarar ve node tanımlarını yükler.
    /// </summary>
    public void ScanPlugins()
    {
        if (!Directory.Exists(_pluginsPath))
        {
            _logger.LogInformation("Plugin dizini bulunamadı: {Path}, oluşturuluyor...", _pluginsPath);
            Directory.CreateDirectory(_pluginsPath);
            return;
        }

        var dllFiles = Directory.GetFiles(_pluginsPath, "*.dll", SearchOption.AllDirectories);
        _logger.LogInformation("Plugin taranıyor: {Count} DLL bulundu ({Path})", dllFiles.Length, _pluginsPath);

        foreach (var dll in dllFiles)
        {
            try
            {
                LoadPluginAssembly(dll);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Plugin yüklenemedi: {Dll}", dll);
            }
        }

        _logger.LogInformation("Toplam {Count} plugin node yüklendi", _pluginNodes.Count);
    }

    /// <summary>
    /// Yüklenen tüm plugin node tanımlarını döndürür.
    /// </summary>
    public List<NodeDefinition> GetPluginNodes() => _pluginNodes.ToList();

    /// <summary>
    /// Belirli bir assembly dosyasını yükler ve node tanımlarını tarar.
    /// </summary>
    public void LoadPluginAssembly(string assemblyPath)
    {
        var assembly = Assembly.LoadFrom(assemblyPath);
        var nodeTypes = ScanAssemblyForNodes(assembly);

        if (nodeTypes.Count > 0)
        {
            _pluginNodes.AddRange(nodeTypes);
            _logger.LogInformation("Plugin yüklendi: {Assembly} ({Count} node)",
                Path.GetFileName(assemblyPath), nodeTypes.Count);
        }
    }

    /// <summary>
    /// Assembly'deki node tanımlarını tarar.
    /// [NodeDefinition] attribute'u ile işaretlenmiş sınıfları arar.
    /// </summary>
    private List<NodeDefinition> ScanAssemblyForNodes(Assembly assembly)
    {
        var nodes = new List<NodeDefinition>();

        try
        {
            foreach (var type in assembly.GetExportedTypes())
            {
                // NodeDefinitionAttribute'u ara
                var nodeAttr = type.GetCustomAttributes()
                    .FirstOrDefault(a => a.GetType().Name == "NodeDefinitionAttribute");
                if (nodeAttr == null) continue;

                // Attribute property'lerini oku
                var attrType = nodeAttr.GetType();
                var nodeType = attrType.GetProperty("NodeType")?.GetValue(nodeAttr)?.ToString() ?? type.Name;
                var nodeName = attrType.GetProperty("DisplayName")?.GetValue(nodeAttr)?.ToString() ?? type.Name;
                var nodeCategory = attrType.GetProperty("Category")?.GetValue(nodeAttr)?.ToString() ?? "Plugin";
                var nodeIcon = attrType.GetProperty("Icon")?.GetValue(nodeAttr)?.ToString() ?? "puzzle";
                var nodeColor = attrType.GetProperty("Color")?.GetValue(nodeAttr)?.ToString() ?? "#89dceb";
                var nodeDescription = attrType.GetProperty("Description")?.GetValue(nodeAttr)?.ToString() ?? "";

                var inputs = new List<NodePort>();
                var outputs = new List<NodePort>();
                var parameters = new List<NodeParameter>();

                // Input attribute'larını tara
                foreach (var attr in type.GetCustomAttributes().Where(a => a.GetType().Name == "NodeInputAttribute"))
                {
                    var aType = attr.GetType();
                    inputs.Add(new NodePort
                    {
                        Name = aType.GetProperty("Name")?.GetValue(attr)?.ToString() ?? "input",
                        Type = aType.GetProperty("DataType")?.GetValue(attr)?.ToString() ?? "any",
                        Multiple = (bool)(aType.GetProperty("Multiple")?.GetValue(attr) ?? false)
                    });
                }

                // Output attribute'larını tara
                foreach (var attr in type.GetCustomAttributes().Where(a => a.GetType().Name == "NodeOutputAttribute"))
                {
                    var aType = attr.GetType();
                    outputs.Add(new NodePort
                    {
                        Name = aType.GetProperty("Name")?.GetValue(attr)?.ToString() ?? "output",
                        Type = aType.GetProperty("DataType")?.GetValue(attr)?.ToString() ?? "any",
                    });
                }

                // Parameter attribute'larını tara
                foreach (var attr in type.GetCustomAttributes().Where(a => a.GetType().Name == "NodeParameterAttribute"))
                {
                    var aType = attr.GetType();
                    parameters.Add(new NodeParameter
                    {
                        Name = aType.GetProperty("Name")?.GetValue(attr)?.ToString() ?? "param",
                        DisplayName = aType.GetProperty("DisplayName")?.GetValue(attr)?.ToString() ?? "Parameter",
                        Type = aType.GetProperty("ParamType")?.GetValue(attr)?.ToString() ?? "string",
                        DefaultValue = aType.GetProperty("DefaultValue")?.GetValue(attr),
                        Required = (bool)(aType.GetProperty("Required")?.GetValue(attr) ?? false)
                    });
                }

                // Default input/output yoksa ekle
                if (inputs.Count == 0)
                    inputs.Add(new NodePort { Name = "input", Type = "any" });
                if (outputs.Count == 0)
                    outputs.Add(new NodePort { Name = "output", Type = "any" });

                nodes.Add(new NodeDefinition
                {
                    Type = $"plugin.{nodeType}",
                    Name = nodeName,
                    Icon = nodeIcon,
                    Category = nodeCategory,
                    Description = nodeDescription,
                    Color = nodeColor,
                    Inputs = inputs,
                    Outputs = outputs,
                    Parameters = parameters
                });
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            _logger.LogWarning("Assembly tür yükleme hatası: {Errors}",
                string.Join("; ", ex.LoaderExceptions?.Select(e => e?.Message) ?? []));
        }

        return nodes;
    }
}
