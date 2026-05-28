using App;

namespace SpaceBattle.Lib
{
    public class ShootCommand : ICommand
    {
        private readonly IShootable _shootable;

        public ShootCommand(IShootable shootable)
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
