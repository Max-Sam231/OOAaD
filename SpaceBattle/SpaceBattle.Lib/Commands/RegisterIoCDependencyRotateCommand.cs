using App;
using SpaceBattle.Lib;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyRotateCommand : ICommand
    {
        public void Execute()
        {
            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Commands.Rotate",
                (Func<object[], object>)(args =>
                {
                    var rotatable = Ioc.Resolve<IRotatingObject>("Adapters.IRotatingObject", args);
                    return new RotateCommand(rotatable);
                })
            ).Execute();
        }
    }
}
