using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SpaceBattle.Lib
{
    public class AdapterFactoryCompiler
    {
        public IAdapterFactory Compile(string source)
        {
            var references = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly.Location.Length > 0)
                .Select(assembly => MetadataReference.CreateFromFile(assembly.Location));

            var compilation = CSharpCompilation.Create(
                "SpaceBattle.Generated." + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(source)],
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var stream = new MemoryStream();
            var result = compilation.Emit(stream);

            if (!result.Success)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));
            }

            stream.Seek(0, SeekOrigin.Begin);
            var assembly = AssemblyLoadContext.Default.LoadFromStream(stream);

            var factoryTypes = assembly.GetTypes()
                .Where(type => type.IsClass && !type.IsAbstract && typeof(IAdapterFactory).IsAssignableFrom(type))
                .ToList();

            if (factoryTypes.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Generated source must declare exactly one concrete IAdapterFactory class, found {factoryTypes.Count}: {string.Join(", ", factoryTypes.Select(type => type.FullName))}.");
            }

            return (IAdapterFactory)Activator.CreateInstance(factoryTypes[0])!;
        }
    }
}
