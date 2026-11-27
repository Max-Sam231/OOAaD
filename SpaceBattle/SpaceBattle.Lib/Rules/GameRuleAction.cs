namespace SpaceBattle.Lib
{
    public sealed class GameRuleAction
    {
        public string Name { get; }

        public IReadOnlyList<string> OrderedSteps { get; }

        public GameRuleAction(string name, IReadOnlyList<string> orderedSteps)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentNullException.ThrowIfNull(orderedSteps);

            Name = name;
            OrderedSteps = orderedSteps;
        }
    }
}
