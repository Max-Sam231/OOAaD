using System;
using App;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyCollisionDetector : ICommand
    {
        public void Execute()
        {
            var cmd = Ioc.Resolve<ICommand>("IoC.Register", "Collision.Detector", (object[] args) =>
            {
                var quadrantSize = (int)args[0];
                return new QuadrantCollisionDetector(quadrantSize);
            });

            cmd.Execute();
        }
    }
}
