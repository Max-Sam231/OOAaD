using App;

namespace SpaceBattle.Lib
{
    public class JournaledCommand : ICommand
    {
        private readonly ICommand _command;
        private readonly PendingCommandJournal _journal;
        private readonly string _id;

        public JournaledCommand(ICommand command, PendingCommandJournal journal, string id)
        {
            _command = command ?? throw new ArgumentNullException(nameof(command));
            _journal = journal ?? throw new ArgumentNullException(nameof(journal));
            _id = id ?? throw new ArgumentNullException(nameof(id));
        }

        public void Execute()
        {
            if (_journal.Remove(_id))
            {
                _command.Execute();
            }
        }
    }
}
