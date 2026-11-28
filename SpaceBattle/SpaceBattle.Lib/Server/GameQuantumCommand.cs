using App;

namespace SpaceBattle.Lib
{
    public class GameQuantumCommand : ICommand
    {
        private readonly ICommand _game;
        private readonly GameQuantumState _state;
        private readonly IGameScheduler _scheduler;

        public GameQuantumCommand(ICommand game, GameQuantumState state, IGameScheduler scheduler)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        }

        public void Execute()
        {
            _state.Start();

            try
            {
                _game.Execute();
            }
            finally
            {
                if (!_state.IsIdle)
                {
                    _scheduler.MakeReady(this);
                }
            }
        }
    }
}
