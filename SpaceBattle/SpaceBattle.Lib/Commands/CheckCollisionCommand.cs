using System;
using App;

namespace SpaceBattle.Lib
{
    public class CheckCollisionCommand : ICommand
    {
        private readonly ICollidable _obj1;
        private readonly ICollidable _obj2;

        public CheckCollisionCommand(ICollidable obj1, ICollidable obj2)
        {
            _obj1 = obj1 ?? throw new ArgumentNullException(nameof(obj1));
            _obj2 = obj2 ?? throw new ArgumentNullException(nameof(obj2));
        }

        public void Execute()
        {
            int dx = _obj1.Position[0] - _obj2.Position[0];
            int dy = _obj1.Position[1] - _obj2.Position[1];

            int ddx = _obj1.Velocity[0] - _obj2.Velocity[0];
            int ddy = _obj1.Velocity[1] - _obj2.Velocity[1];

            bool isCollision = Ioc.Resolve<bool>("Game.Collision.Check", dx, dy, ddx, ddy);

            if (isCollision)
            {
                Ioc.Resolve<ICommand>("Game.Collision.Event", _obj1, _obj2).Execute();
            }
        }
    }
}
