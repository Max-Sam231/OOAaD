using App;

namespace SpaceBattle.Lib
{
    public sealed class ThreadGameEndpointRoute : IGameEndpointRoute
    {
        private readonly Func<ICommand, bool> _tryEnqueue;
        private readonly Func<CommandDescription, ICommand> _deliverFactory;

        public ThreadGameEndpointRoute(
            Func<ICommand, bool> tryEnqueue,
            Func<CommandDescription, ICommand> deliverFactory)
        {
            ArgumentNullException.ThrowIfNull(tryEnqueue);
            ArgumentNullException.ThrowIfNull(deliverFactory);

            _tryEnqueue = tryEnqueue;
            _deliverFactory = deliverFactory;
        }

        public bool TryRoute(CommandDescription message)
        {
            ArgumentNullException.ThrowIfNull(message);

            return _tryEnqueue(_deliverFactory(message));
        }
    }
}
