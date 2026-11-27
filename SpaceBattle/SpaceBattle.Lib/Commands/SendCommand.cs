using App;

namespace SpaceBattle.Lib
{
    public class SendCommand : ICommand
    {
        private readonly ICommand _command;
        private readonly ICommandReceiver _receiver;

        public SendCommand(ICommand command, ICommandReceiver receiver)
        {
            _command = command ?? throw new ArgumentNullException(nameof(command));
            _receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
        }
        public void Execute()
        {
            _receiver.Receive(_command);
        }
    }
}
