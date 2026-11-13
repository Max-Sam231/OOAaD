using App;

namespace SpaceBattle.Lib
{
    public class ShootCommand : ICommand
    {
        private readonly IShootingObject _shootable;

        public ShootCommand(IShootingObject shootable)
        {
            _shootable = shootable ?? throw new ArgumentNullException(nameof(shootable));
        }

        public void Execute()
        {
            var projectileCommand = _shootable.ProjectileCommand ?? throw new InvalidOperationException();
            var receiver = _shootable.Receiver ?? throw new InvalidOperationException();

            receiver.Receive(projectileCommand);
        }
    }
}
