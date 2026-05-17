namespace SpaceBattle.Lib
{
    public class CommandInjectableCommand : ICommand, ICommandInjectable
    {
        private ICommand? _injectedCommand;

        public void Execute()
        {
            if (_injectedCommand == null)
            {
                throw new InvalidOperationException("");
            }

            _injectedCommand.Execute();
        }

        public void Inject(ICommand command)
        {
            _injectedCommand = command;
        }
    }
}
