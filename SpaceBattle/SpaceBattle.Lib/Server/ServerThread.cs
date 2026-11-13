using System.Collections.Concurrent;
using App;

namespace SpaceBattle.Lib
{
    public class ServerThread : ICommandReceiver
    {
        private readonly Thread _thread;
        private readonly BlockingCollection<ICommand> _inbox;

        public ServerThread(Thread thread, BlockingCollection<ICommand> inbox, IGameScheduler scheduler, ServerThreadState state)
        {
            _thread = thread ?? throw new ArgumentNullException(nameof(thread));
            _inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));
            Scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        public IGameScheduler Scheduler { get; }

        public ServerThreadState State { get; }

        public void Receive(ICommand command)
        {
            _inbox.Add(command);
        }

        public void Start()
        {
            _thread.Start();
        }

        public bool Join(TimeSpan timeout)
        {
            return _thread.Join(timeout);
        }
    }
}
