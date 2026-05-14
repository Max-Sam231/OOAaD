using App;
using SpaceBattle.Lib;
using ICommandApp = App.ICommand;

namespace SpaceBattle.Lib
{   
    public class RegisterIoCDependencyRotateCommand : ICommand
    {
        public void Execute()
        {
            Ioc.Resolve<ICommandApp>(
                "IoC.Register",
                "Commands.Rotate",
                (Func<object[], object>)(args =>
                {
                    var rotatable = Ioc.Resolve<IRotatable>("Adapters.IRotatable", args);
                    return new RotateCommand(rotatable);
                })
            ).Execute();
        }
    }
}
