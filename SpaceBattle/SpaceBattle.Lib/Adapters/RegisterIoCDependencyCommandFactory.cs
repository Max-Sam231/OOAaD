using System.Collections.Concurrent;
using App;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyCommandFactory : ICommand
    {
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
                    type => compiler.Compile<ICommandFactory>(builder.Build(type))))
            ).Execute();
        }
    }
}
