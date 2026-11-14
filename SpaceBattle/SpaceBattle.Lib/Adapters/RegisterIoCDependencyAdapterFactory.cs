using System.Collections.Concurrent;
using App;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyAdapterFactory : ICommand
    {
        public void Execute()
        {
            new RegisterIoCDependencyAdapterTemplates().Execute();

            var builder = new AdapterSourceBuilder();
            var compiler = new GeneratedCodeCompiler();
            var factories = new ConcurrentDictionary<Type, IAdapterFactory>();

            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Adapters.Factory",
                (Func<object[], object>)(args => factories.GetOrAdd(
                    (Type)args[0],
                    type => compiler.Compile<IAdapterFactory>(builder.Build(type))))
            ).Execute();
        }
    }
}
