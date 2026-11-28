namespace SpaceBattle.Lib
{
    public class DelegatingGameCommandSending : IGameCommandSending
    {
        private readonly Action<CommandDescription> _send;

        public DelegatingGameCommandSending(Action<CommandDescription> send)
        {
            _send = send ?? throw new ArgumentNullException(nameof(send));
        }

        public void Send(CommandDescription description)
        {
            _send(description);
        }
    }
}
