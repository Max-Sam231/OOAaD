using System;
using System.Collections.Generic;
using App;

namespace SpaceBattle.Lib
{
    public class CheckSectorCollisionsCommand : ICommand
    {
        private readonly ICollidable _obj;

        public CheckSectorCollisionsCommand(ICollidable obj)
        {
            _obj = obj ?? throw new ArgumentNullException(nameof(obj));
        }

        public void Execute()
        {
            var neighbors = Ioc.Resolve<IEnumerable<ICollidable>>("Game.Grid.GetNeighbors", _obj);

            foreach (var neighbor in neighbors)
            {
                if (neighbor == _obj) continue;

                Ioc.Resolve<ICommand>("Commands.CheckCollision", _obj, neighbor).Execute();
            }
        }
    }
}
