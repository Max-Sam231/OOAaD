namespace SpaceBattle.Lib
{
    public class TransferableGame
    {
        public TransferableGame(
            ServerThread thread,
            IGameCommandSending sending,
            RedirectingGameCommandSending location,
            PendingCommandJournal journal,
            EnumerableGameObjectRepository repository,
            string rules,
            int commandLimit)
        {
            Thread = thread;
            Sending = sending;
            Location = location;
            Journal = journal;
            Repository = repository;
            Rules = rules;
            CommandLimit = commandLimit;
        }

        public ServerThread Thread { get; }

        public IGameCommandSending Sending { get; }

        public RedirectingGameCommandSending Location { get; }

        public PendingCommandJournal Journal { get; }

        public EnumerableGameObjectRepository Repository { get; }

        public string Rules { get; }

        public int CommandLimit { get; }

        public TransferableGame? Successor { get; set; }
    }
}
