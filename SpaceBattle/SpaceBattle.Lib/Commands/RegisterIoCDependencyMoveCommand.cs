using App;
using SpaceBattle.Lib;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyMoveCommand : ICommand
    {
        public void Execute()
        {
            var cmd = Ioc.Resolve<ICommand>("IoC.Register", "Commands.Move", (object[] args) =>
            {
                var movable = Ioc.Resolve<IMovable>("Adapters.IMovable", args);
                return new MoveCommand(movable);
            });

            cmd.Execute();
        }
    }
}
