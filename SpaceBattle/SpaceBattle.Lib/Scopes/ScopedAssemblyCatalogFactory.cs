using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;

namespace SpaceBattle.Lib
{
    public static class ScopedAssemblyCatalogFactory
    {
        public static ScopedAssemblyCatalog FromAssemblies(IEnumerable<Assembly> assemblies)
        {
            ArgumentNullException.ThrowIfNull(assemblies);

            var assemblyList = assemblies.ToList();
            var references = new List<MetadataReference>();

            foreach (var assembly in assemblyList)
            {
                if (assembly.Location.Length > 0)
                {
                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
                }
            }

            return new ScopedAssemblyCatalog(assemblyList, references);
        }

        public static ScopedAssemblyCatalog CreateDefault()
        {
            return FromAssemblies(AssemblyLoadContext.Default.Assemblies);
        }

        public static ScopedAssemblyCatalog CreateWithPlugins(
            IReadOnlyList<Assembly> pluginAssemblies,
            IReadOnlyList<byte[]> pluginImages)
        {
            ArgumentNullException.ThrowIfNull(pluginAssemblies);
            ArgumentNullException.ThrowIfNull(pluginImages);

            var baseCatalog = CreateDefault();
            var assemblies = new List<Assembly>(baseCatalog.Assemblies.Count + pluginAssemblies.Count);
            assemblies.AddRange(baseCatalog.Assemblies);
            assemblies.AddRange(pluginAssemblies);

            var references = new List<MetadataReference>(baseCatalog.MetadataReferences.Count + pluginImages.Count);
            references.AddRange(baseCatalog.MetadataReferences);

            foreach (var image in pluginImages)
            {
                references.Add(MetadataReference.CreateFromImage(image));
            }

            return new ScopedAssemblyCatalog(assemblies, references);
        }
    }
}
