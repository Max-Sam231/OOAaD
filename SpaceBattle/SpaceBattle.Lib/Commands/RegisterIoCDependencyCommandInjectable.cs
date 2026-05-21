using App;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyCommandInjectable : ICommand
    {
        public void Execute()
        {
            var cmd = Ioc.Resolve<App.ICommand>("IoC.Register", "Commands.CommandInjectable", (object[] args) =>
            {
                return new CommandInjectableCommand();
            });

            cmd.Execute();
        }
    }
}
