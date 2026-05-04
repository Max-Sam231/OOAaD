using SpaceBattle.Lib;

namespace SpaceBattle.Lib
{
    public class MoveCommand : ICommand
    {

        private readonly IMovable _movable;

        public MoveCommand(IMovable movable)
        {
            _movable = movable ?? throw new ArgumentNullException(nameof(movable));
        }

        public void Execute()
        {

            var position = _movable.Position ?? throw new InvalidOperationException();
            var velocity = _movable.Velocity ?? throw new InvalidOperationException();

            var newPosition = position + velocity;

            try
            {
                _movable.Position = newPosition;
            }

            catch (Exception ex)
            {
                throw new InvalidOperationException("", ex);
            }
        }
    }
}
