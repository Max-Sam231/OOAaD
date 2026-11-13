using App;

namespace SpaceBattle.Lib
{
    public class DelegatingCommandReceiver : ICommandReceiver
    {
        private readonly Action<ICommand> _receive;

        public DelegatingCommandReceiver(Action<ICommand> receive)
        {
            _receive = receive ?? throw new ArgumentNullException(nameof(receive));
        }

        public void Receive(ICommand command)
        {
            _receive(command);
        }
    }
}
