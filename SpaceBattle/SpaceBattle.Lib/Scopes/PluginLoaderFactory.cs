using System.Reflection;
using System.Runtime.Loader;
using App;

namespace SpaceBattle.Lib
{
    public sealed class PluginLoaderFactory
    {
        public PluginBatch Load(string pluginsDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginsDirectory);

            if (!Directory.Exists(pluginsDirectory))
            {
                throw new DirectoryNotFoundException($"Plugins directory '{pluginsDirectory}' does not exist.");
            }

            var loadContext = new AssemblyLoadContext("SpaceBattle.Plugins." + Guid.NewGuid().ToString("N"), isCollectible: true);
            var dllPaths = Directory.GetFiles(pluginsDirectory, "*.dll", SearchOption.TopDirectoryOnly);
            Array.Sort(dllPaths, StringComparer.Ordinal);

            var assemblies = new List<Assembly>(dllPaths.Length);
            var images = new List<byte[]>(dllPaths.Length);
            var registrationCommands = new List<ICommand>();

            foreach (var dllPath in dllPaths)
            {
                var image = File.ReadAllBytes(dllPath);
                using var stream = new MemoryStream(image);
                var assembly = loadContext.LoadFromStream(stream);
                assemblies.Add(assembly);
                images.Add(image);
            }

            foreach (var assembly in assemblies)
            {
                var registrationTypes = assembly.GetTypes()
                    .Where(IsRegistrationCommand)
                    .OrderBy(type => type.FullName, StringComparer.Ordinal);

                foreach (var type in registrationTypes)
                {
                    registrationCommands.Add((ICommand)Activator.CreateInstance(type)!);
                }
            }

            var catalog = ScopedAssemblyCatalogFactory.CreateWithPlugins(assemblies, images);
            return new PluginBatch(catalog, registrationCommands);
        }

        private static bool IsRegistrationCommand(Type type)
        {
            return type.IsClass
                && !type.IsAbstract
                && typeof(ICommand).IsAssignableFrom(type)
                && type.Name.StartsWith("Register", StringComparison.Ordinal)
                && type.GetConstructor(Type.EmptyTypes) != null;
        }
    }
}
