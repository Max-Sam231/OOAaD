using System.Collections.Concurrent;
using App;

namespace SpaceBattle.Lib
{
    public class ServerThreadState
    {
        private readonly BlockingCollection<ICommand> _inbox;
        private readonly IGameScheduler _scheduler;
        private bool _hardStopRequested;
        private bool _softStopRequested;

        public ServerThreadState(BlockingCollection<ICommand> inbox, IGameScheduler scheduler)
        {
            _inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        }

        public bool CanContinue =>
            !_hardStopRequested && !(_softStopRequested && _inbox.Count == 0 && !_scheduler.HasReady);

        public void RequestHardStop()
        {
            _hardStopRequested = true;
        }

        public void RequestSoftStop()
        {
            _softStopRequested = true;
        }
    }
}
