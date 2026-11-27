using System.Collections.Concurrent;
using App;

namespace SpaceBattle.Lib
{
    public class ServerThreadLoopCommand : ICommand
    {
        private readonly BlockingCollection<ICommand> _inbox;
        private readonly IGameScheduler _scheduler;
        private readonly ServerThreadState _state;
        private bool _inboxTurn;

        public ServerThreadLoopCommand(BlockingCollection<ICommand> inbox, IGameScheduler scheduler, ServerThreadState state)
        {
            _inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public void Execute()
        {
            while (_state.CanContinue)
            {
                var command = Next();

                try
                {
                    command.Execute();
                }
                catch (Exception exception)
                {
                    Ioc.Resolve<ICommand>("Exception.Handle", command, exception).Execute();
                }
            }
        }

        private ICommand Next()
        {
            _inboxTurn = !_inboxTurn;

            if (!_scheduler.HasReady)
            {
                return _inbox.Take();
            }

            if (_inboxTurn && _inbox.TryTake(out var message))
            {
                return message;
            }

            _scheduler.TryTakeReady(out var game);

            return game!;
        }
    }
}
