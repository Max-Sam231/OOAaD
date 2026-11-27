using System.Collections.Concurrent;
using System.Runtime.Loader;
using App;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyAdapterFactory : ICommand
    {
        private readonly ScopedAssemblyCatalog _catalog;

        public RegisterIoCDependencyAdapterFactory()
            : this(ScopedAssemblyCatalogFactory.CreateDefault())
        {
        }

        public RegisterIoCDependencyAdapterFactory(ScopedAssemblyCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            _catalog = catalog;
        }

        public void Execute()
        {
            new RegisterIoCDependencyAdapterTemplates().Execute();

            var builder = new AdapterSourceBuilder();
            var compiler = new GeneratedCodeCompiler();
            var factories = new ConcurrentDictionary<Type, IAdapterFactory>();

            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Adapters.Assemblies",
                (Func<object[], object>)(_ => _catalog)
            ).Execute();

            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Adapters.Factory",
                (Func<object[], object>)(args => factories.GetOrAdd(
                    (Type)args[0],
                    type => compiler.Compile<IAdapterFactory>(
                        builder.Build(type),
                        AssemblyLoadContext.GetLoadContext(type.Assembly)!,
                        _catalog.MetadataReferences)))
            ).Execute();
        }
    }
}
