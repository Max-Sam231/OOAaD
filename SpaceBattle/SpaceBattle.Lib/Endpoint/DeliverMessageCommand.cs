using App;

namespace SpaceBattle.Lib
{
    public sealed class DeliverMessageCommand : ICommand
    {
        private readonly IGameReceiverRegistry _receiverRegistry;
        private readonly CommandDescription _message;

        public DeliverMessageCommand(IGameReceiverRegistry receiverRegistry, CommandDescription message)
        {
            ArgumentNullException.ThrowIfNull(receiverRegistry);
            ArgumentNullException.ThrowIfNull(message);

            _receiverRegistry = receiverRegistry;
            _message = message;
        }

        public void Execute()
        {
            if (!_receiverRegistry.TryGet(_message.GameId, out var target))
            {
                return;
            }

            target.Send(_message);
        }
    }
}
