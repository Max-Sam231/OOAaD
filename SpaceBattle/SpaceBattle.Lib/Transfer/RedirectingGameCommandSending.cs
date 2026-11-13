namespace SpaceBattle.Lib
{
    public class RedirectingGameCommandSending : IGameCommandSending
    {
        private IGameCommandSending _target;

        public RedirectingGameCommandSending(IGameCommandSending target)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public void RedirectTo(IGameCommandSending target)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public void Send(CommandDescription description)
        {
            _target.Send(description);
        }
    }
}
