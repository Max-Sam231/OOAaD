using App;

namespace SpaceBattle.Lib
{
    public class SchedulingCommandReceiver : ICommandReceiver
    {
        private readonly ICommandReceiver _receiver;
        private readonly IGameScheduler _scheduler;
        private readonly ICommand _gameTurn;

        public SchedulingCommandReceiver(ICommandReceiver receiver, IGameScheduler scheduler, ICommand gameTurn)
        {
            _receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _gameTurn = gameTurn ?? throw new ArgumentNullException(nameof(gameTurn));
        }

        public void Receive(ICommand command)
        {
            _receiver.Receive(command);
            _scheduler.MakeReady(_gameTurn);
        }
    }
}
