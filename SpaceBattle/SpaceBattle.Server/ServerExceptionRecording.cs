using System.Collections.Concurrent;
using App;

namespace SpaceBattle.Server
{
    public sealed class ServerExceptionRecording : IServerExceptionRecording
    {
        private readonly ConcurrentQueue<Exception> _exceptions = new ConcurrentQueue<Exception>();

        public void Record(ICommand command, Exception exception)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(exception);

            _exceptions.Enqueue(exception);
        }

        public IReadOnlyCollection<Exception> Snapshot()
        {
            return _exceptions.ToArray();
        }
    }
}
