using App;

namespace SpaceBattle.Lib
{
    public class MarkGameIdleCommand : ICommand
    {
        private readonly GameQuantumState _state;

        public MarkGameIdleCommand(GameQuantumState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public void Execute()
        {
            _state.MarkIdle();
        }
    }
}
