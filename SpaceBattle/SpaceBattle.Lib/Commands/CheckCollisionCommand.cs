using System;
using App;

namespace SpaceBattle.Lib
{
    public class CheckCollisionCommand : ICommand
    {
        private readonly ICollidable _a;
        private readonly ICollidable _b;
        private readonly ICollisionDetector _detector;

        public CheckCollisionCommand(ICollidable a, ICollidable b, ICollisionDetector detector)
        {
            _a = a ?? throw new ArgumentNullException(nameof(a));
            _b = b ?? throw new ArgumentNullException(nameof(b));
            _detector = detector ?? throw new ArgumentNullException(nameof(detector));
        }

        public void Execute()
        {
            if (_detector.DetectCollision(_a, _b))
            {
                Ioc.Resolve<ICommand>("Events.Collision", _a, _b).Execute();
            }
        }
    }
}
