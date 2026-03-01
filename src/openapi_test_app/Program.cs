using System;
using System.Linq;
using System.Reflection;

class Program
{
    static void Main()
    {
        var types = typeof(Microsoft.AspNetCore.OpenApi.IOpenApiDocumentTransformer).Assembly
            .GetReferencedAssemblies()
            .Select(Assembly.Load)
            .SelectMany(a => a.GetTypes())
            .Where(t => t.Name.Contains("OperationType") || t.Name.Contains("OpenApiOperation") || t.Name.Contains("OpenApiDocument"));

        foreach (var t in types)
            Console.WriteLine(t.FullName + " IN " + t.Assembly.GetName().Name);
    }
}
