using App;

namespace SpaceBattle.Lib
{
    public class LocalGameCommandSending : IGameCommandSending
    {
        private readonly ICommandReceiver _game;
        private readonly PendingCommandJournal _journal;
        private readonly Func<CommandDescription, ICommand> _createCommand;

        public LocalGameCommandSending(
            ICommandReceiver game,
            PendingCommandJournal journal,
            Func<CommandDescription, ICommand> createCommand)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _journal = journal ?? throw new ArgumentNullException(nameof(journal));
            _createCommand = createCommand ?? throw new ArgumentNullException(nameof(createCommand));
        }

        public void Send(CommandDescription description)
        {
            _journal.Add(description);
            _game.Receive(_createCommand(description));
        }
    }
}
