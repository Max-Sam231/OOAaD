using App;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyGameQuantum : ICommand
    {
        private readonly GameQuantumState _state;

        public RegisterIoCDependencyGameQuantum(GameQuantumState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public void Execute()
        {
            var markIdle = new MarkGameIdleCommand(_state);

            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Game.Loop.CanContinue",
                (Func<object[], object>)(args => _state.CanContinue())
            ).Execute();

            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Game.Loop.Idle",
                (Func<object[], object>)(args => markIdle)
            ).Execute();
        }
    }
}
