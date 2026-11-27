using App;

namespace SpaceBattle.Lib
{
    public sealed class LocalEndpointGameSending : IGameCommandSending
    {
        private readonly ICommandReceiver _receiver;
        private readonly Func<CommandDescription, ICommand> _commandFactory;

        public LocalEndpointGameSending(
            ICommandReceiver receiver,
            Func<CommandDescription, ICommand> commandFactory)
        {
            ArgumentNullException.ThrowIfNull(receiver);
            ArgumentNullException.ThrowIfNull(commandFactory);

            _receiver = receiver;
            _commandFactory = commandFactory;
        }

        public void Send(CommandDescription description)
        {
            ArgumentNullException.ThrowIfNull(description);

            _receiver.Receive(_commandFactory(description));
        }
    }
}
