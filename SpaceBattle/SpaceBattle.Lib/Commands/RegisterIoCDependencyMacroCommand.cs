using App;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyMacroCommand: ICommand
    {
        public void Execute()
        {
            var cmd = Ioc.Resolve<ICommand>("IoC.Register", "Commands.Macro", (object[] args) =>
            {
               var commands = (ICommand[])args[0];
               return new MacroCommand(commands);
            });

            cmd.Execute();
        }
    }
}