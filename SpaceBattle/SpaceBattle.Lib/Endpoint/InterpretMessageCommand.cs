using App;

namespace SpaceBattle.Lib
{
    public sealed class InterpretMessageCommand : ICommand
    {
        private readonly CommandDescription _description;

        public InterpretMessageCommand(CommandDescription description)
        {
            ArgumentNullException.ThrowIfNull(description);

            _description = description;
        }

        public void Execute()
        {
            if (!Ioc.Resolve<bool>("Game.Step.Validate", _description))
            {
                return;
            }

            Ioc.Resolve<ICommand>("Game.Message.Command", _description).Execute();
            Ioc.Resolve<ICommand>("Game.Step.Advance").Execute();
            Ioc.Resolve<ICommand>("Game.State.Broadcast", _description.GameId).Execute();
        }
    }
}
