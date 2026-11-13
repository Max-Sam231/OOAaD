using System.Collections.Concurrent;
using App;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyAdapterFactory : ICommand
    {
        public void Execute()
        {
            var builder = new AdapterSourceBuilder();
            var compiler = new AdapterFactoryCompiler();
            var factories = new ConcurrentDictionary<Type, IAdapterFactory>();

            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Adapters.Factory",
                (Func<object[], object>)(args => factories.GetOrAdd(
                    (Type)args[0],
                    type => compiler.Compile(builder.Build(type), AdapterDependencyNaming.FactoryTypeName(type))))
            ).Execute();
        }
    }
}
