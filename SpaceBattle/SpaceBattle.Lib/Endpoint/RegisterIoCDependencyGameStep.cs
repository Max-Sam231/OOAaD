using App;

namespace SpaceBattle.Lib
{
    public sealed class RegisterIoCDependencyGameStep : ICommand
    {
        private readonly long _maxFutureDelta;

        public RegisterIoCDependencyGameStep(long maxFutureDelta = 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(maxFutureDelta);

            _maxFutureDelta = maxFutureDelta;
        }

        public void Execute()
        {
            var state = new GameStepState(_maxFutureDelta);
            var validator = new NonBlockingStepValidating(state);

            Register("Game.Step.State", _ => state);
            Register("Game.Step.Current", _ => state.CurrentStep);
            Register("Game.Step.Validate", args => validator.Validate((CommandDescription)args[0]));
            Register("Game.Step.Advance", _ => new DelegatingCommand(state.Advance));
        }

        private static void Register(string dependency, Func<object[], object> strategy)
        {
            Ioc.Resolve<ICommand>("IoC.Register", dependency, strategy).Execute();
        }
    }
}
