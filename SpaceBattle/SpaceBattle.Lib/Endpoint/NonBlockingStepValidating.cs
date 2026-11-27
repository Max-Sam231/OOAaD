namespace SpaceBattle.Lib
{
    public sealed class NonBlockingStepValidating : IStepValidating
    {
        private readonly GameStepState _state;

        public NonBlockingStepValidating(GameStepState state)
        {
            ArgumentNullException.ThrowIfNull(state);

            _state = state;
        }

        public bool Validate(CommandDescription message)
        {
            ArgumentNullException.ThrowIfNull(message);

            return message.Step >= _state.CurrentStep
                && message.Step <= _state.CurrentStep + _state.MaxFutureDelta;
        }
    }
}
