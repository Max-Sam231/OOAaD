using App;

namespace SpaceBattle.Lib
{
    public class SoftStopServerThreadCommand : ICommand
    {
        private readonly ServerThreadState _state;

        public SoftStopServerThreadCommand(ServerThreadState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public void Execute()
        {
            _state.RequestSoftStop();
        }
    }
}
