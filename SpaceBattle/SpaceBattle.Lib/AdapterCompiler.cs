using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SpaceBattle.Lib
{
    public static class AdapterCompiler
    {
        public static Type Compile(string sourceCode, Type interfaceType)
        {
            var className = $"{interfaceType.Name}Adapter";
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

            var references = new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.IDictionary<,>).Assembly.Location),
                MetadataReference.CreateFromFile(interfaceType.Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("Core").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                MetadataReference.CreateFromFile(typeof(SpaceBattle.Lib.Vector).Assembly.Location)
            };

            var compilation = CSharpCompilation.Create(
                $"GeneratedAssembly_{Guid.NewGuid():N}",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);

            if (!result.Success)
            {
                var errors = string.Join("\n", result.Diagnostics);
                throw new InvalidOperationException($"Ошибка компиляции адаптера:\n{errors}");
            }

            ms.Seek(0, SeekOrigin.Begin);
            var assembly = AssemblyLoadContext.Default.LoadFromStream(ms);
            return assembly.GetType($"SpaceBattle.Lib.Generated.{className}")!;
        }
    }
}
