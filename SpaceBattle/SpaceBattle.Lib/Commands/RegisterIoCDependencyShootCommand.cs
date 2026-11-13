using App;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyShootCommand : ICommand
    {
        public void Execute()
        {
            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Commands.Shoot",
                (Func<object[], object>)(args =>
                {
                    var shootable = Ioc.Resolve<IShootingObject>("Adapters.IShootingObject", args);
                    return new ShootCommand(shootable);
                })
            ).Execute();
        }
    }
}
