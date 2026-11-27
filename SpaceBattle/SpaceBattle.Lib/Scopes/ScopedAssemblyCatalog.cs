using System.Reflection;
using Microsoft.CodeAnalysis;

namespace SpaceBattle.Lib
{
    public sealed class ScopedAssemblyCatalog
    {
        public IReadOnlyList<Assembly> Assemblies { get; }

        public IReadOnlyList<MetadataReference> MetadataReferences { get; }

        public ScopedAssemblyCatalog(
            IReadOnlyList<Assembly> assemblies,
            IReadOnlyList<MetadataReference> metadataReferences)
        {
            ArgumentNullException.ThrowIfNull(assemblies);
            ArgumentNullException.ThrowIfNull(metadataReferences);

            Assemblies = assemblies;
            MetadataReferences = metadataReferences;
        }
    }
}
