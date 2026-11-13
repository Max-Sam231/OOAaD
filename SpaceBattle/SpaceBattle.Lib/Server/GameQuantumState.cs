namespace SpaceBattle.Lib
{
    public class GameQuantumState
    {
        private readonly int _commandLimit;
        private int _executed;

        public GameQuantumState(int commandLimit)
        {
            if (commandLimit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(commandLimit));
            }

            _commandLimit = commandLimit;
        }

        public bool IsIdle { get; private set; }

        public void Start()
        {
            _executed = 0;
            IsIdle = false;
        }

        public void MarkIdle()
        {
            IsIdle = true;
        }

        public bool CanContinue()
        {
            if (IsIdle || _executed >= _commandLimit)
            {
                return false;
            }

            _executed++;

            return true;
        }
    }
}
