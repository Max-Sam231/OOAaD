using System;

namespace SpaceBattle.Core
{
    public class RegisterIoCDependencyMoveCommand : Icommand
    {
        public void Execute()
        {
            Ioc.Register("Commands.Move", (object obj) => 
            {
                var movable = Ioc.Resolve<Imovable>("Adapters.IMovable", obj);

                return new MoveCommand(movable);
            });
        }
    }
}