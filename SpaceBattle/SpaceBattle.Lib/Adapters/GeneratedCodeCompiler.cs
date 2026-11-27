using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SpaceBattle.Lib
{
    public class GeneratedCodeCompiler
    {
        public T Compile<T>(string source)
        {
            return Compile<T>(
                source,
                AssemblyLoadContext.Default,
                ScopedAssemblyCatalogFactory.CreateDefault().MetadataReferences);
        }

        public T Compile<T>(
            string source,
            AssemblyLoadContext targetLoadContext,
            IEnumerable<MetadataReference> references)
        {
            ArgumentNullException.ThrowIfNull(targetLoadContext);
            ArgumentNullException.ThrowIfNull(references);

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
            var assembly = targetLoadContext.LoadFromStream(stream);

            var factoryTypes = assembly.GetTypes()
                .Where(type => type.IsClass && !type.IsAbstract && typeof(T).IsAssignableFrom(type))
                .ToList();

            if (factoryTypes.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Generated source must declare exactly one concrete {typeof(T).Name} class, found {factoryTypes.Count}: {string.Join(", ", factoryTypes.Select(type => type.FullName))}.");
            }

            return (T)Activator.CreateInstance(factoryTypes[0])!;
        }
    }
}
