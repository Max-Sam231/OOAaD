using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SpaceBattle.Lib
{
    public class AdapterFactoryCompiler
    {
        public IAdapterFactory Compile(string source, string factoryTypeName)
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

            return (IAdapterFactory)Activator.CreateInstance(assembly.GetType(factoryTypeName, true)!)!;
        }
    }
}
