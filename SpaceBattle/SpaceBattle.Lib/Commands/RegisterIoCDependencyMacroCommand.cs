using App;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyMacroCommand : ICommand
    {
        public void Execute()
        {
            var cmd = Ioc.Resolve<ICommand>("IoC.Register", "Commands.Macro", (object[] args) =>
            {
                var commands = ((IEnumerable<ICommand>)args[0]).ToArray();
                return new MacroCommand(commands);
            });

            cmd.Execute();
        }
    }
}
