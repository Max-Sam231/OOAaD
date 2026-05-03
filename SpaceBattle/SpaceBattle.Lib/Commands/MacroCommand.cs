namespace SpaceBattle.Lib
{
    public class MacroCommand : ICommand
    {
        private readonly ICommand[] _commands;
        public MacroCommand(ICommand[] commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        public void Execute()
        {
            ExecuteStep(0);
        }

        private void ExecuteStep(int index)
        {
            if (index >= _commands.Length) return;

            _commands[index].Execute();

            ExecuteStep(index + 1);
        }
    }
}
