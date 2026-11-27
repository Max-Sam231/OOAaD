namespace SpaceBattle.Lib
{
    public sealed class GameStepState
    {
        public GameStepState(long maxFutureDelta = 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(maxFutureDelta);

            MaxFutureDelta = maxFutureDelta;
        }

        public long CurrentStep { get; private set; }

        public long MaxFutureDelta { get; }

        public void Advance()
        {
            CurrentStep++;
        }

        public void Restore(long currentStep)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(currentStep);

            CurrentStep = currentStep;
        }
    }
}
