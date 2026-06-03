using System;
using App;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyCheckCollisionCommand : ICommand
    {
        public void Execute()
        {
            var cmd = Ioc.Resolve<ICommand>("IoC.Register", "Commands.CheckCollision", (object[] args) =>
            {
                var a = (ICollidable)args[0];
                var b = (ICollidable)args[1];
                var detector = (ICollisionDetector)args[2];
                return new CheckCollisionCommand(a, b, detector);
            });

            cmd.Execute();
        }
    }
}
