using App;

namespace SpaceBattle.Lib
{
    public class DelegatingCommand : ICommand
    {
        private readonly Action _execute;

        public DelegatingCommand(Action execute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public void Execute()
        {
            _execute();
        }
    }
}
