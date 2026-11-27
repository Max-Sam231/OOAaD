using System.Collections.Concurrent;
using System.Runtime.Loader;
using App;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyCommandFactory : ICommand
    {
        private readonly ScopedAssemblyCatalog _catalog;

        public RegisterIoCDependencyCommandFactory()
            : this(ScopedAssemblyCatalogFactory.CreateDefault())
        {
        }

        public RegisterIoCDependencyCommandFactory(ScopedAssemblyCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            _catalog = catalog;
        }

        public void Execute()
        {
            var builder = new CommandFactorySourceBuilder();
            var compiler = new GeneratedCodeCompiler();
            var factories = new ConcurrentDictionary<Type, ICommandFactory>();

            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Commands.Factory",
                (Func<object[], object>)(args => factories.GetOrAdd(
                    (Type)args[0],
                    type => compiler.Compile<ICommandFactory>(
                        builder.Build(type),
                        AssemblyLoadContext.GetLoadContext(type.Assembly)!,
                        _catalog.MetadataReferences)))
            ).Execute();
        }
    }
}
