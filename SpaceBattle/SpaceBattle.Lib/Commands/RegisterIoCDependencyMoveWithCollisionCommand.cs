using System;
using System.Collections.Generic;
using App;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyMoveWithCollisionCommand : ICommand
    {
        public void Execute()
        {
            Ioc.Resolve<ICommand>("IoC.Register", "Commands.MoveWithCollision", (Func<object[], object>)(args => 
            {
                var obj = (ICollidable)args[0];

                var commands = new List<ICommand>
                {
                    Ioc.Resolve<ICommand>("Commands.Move", obj),
                    new UpdateSectorCommand(obj),
                    new CheckSectorCollisionsCommand(obj)
                };

                return Ioc.Resolve<ICommand>("Commands.Macro", commands);

            })).Execute();
        }
    }
}
