using App;

namespace SpaceBattle.Lib
{
    public class HardStopServerThreadCommand : ICommand
    {
        private readonly ServerThreadState _state;

        public HardStopServerThreadCommand(ServerThreadState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public void Execute()
        {
            _state.RequestHardStop();
        }
    }
}
