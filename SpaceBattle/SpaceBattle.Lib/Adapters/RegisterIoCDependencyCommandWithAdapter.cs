using App;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyCommandWithAdapter : ICommand
    {
        private readonly string _dependencyName;
        private readonly Type _commandType;

        public RegisterIoCDependencyCommandWithAdapter(string dependencyName, Type commandType)
        {
            _dependencyName = dependencyName ?? throw new ArgumentNullException(nameof(dependencyName));
            _commandType = commandType ?? throw new ArgumentNullException(nameof(commandType));
        }

        public void Execute()
        {
            var factory = Ioc.Resolve<ICommandFactory>("Commands.Factory", _commandType);

            Ioc.Resolve<ICommand>(
                "IoC.Register",
                _dependencyName,
                (Func<object[], object>)(args => factory.Create((IDictionary<string, object>)args[0]))
            ).Execute();
        }
    }
}
