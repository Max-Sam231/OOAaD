using System.Text.Json;

namespace SpaceBattle.Lib
{
    public sealed class GameRulesConfig
    {
        public string Version { get; }

        public string RawJson { get; }

        public string? VictoryTarget { get; }

        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, Dictionary<string, JsonElement>>> Templates { get; }

        public IReadOnlyDictionary<string, string> Collisions { get; }

        public IReadOnlyList<GameRuleEntity> Entities { get; }

        public IReadOnlyList<GameRuleAction> Actions { get; }

        public GameRulesConfig(
            string version,
            string rawJson,
            IReadOnlyList<GameRuleEntity> entities,
            IReadOnlyList<GameRuleAction> actions,
            string? victoryTarget = null,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, Dictionary<string, JsonElement>>>? templates = null,
            IReadOnlyDictionary<string, string>? collisions = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(version);
            ArgumentException.ThrowIfNullOrWhiteSpace(rawJson);
            ArgumentNullException.ThrowIfNull(entities);
            ArgumentNullException.ThrowIfNull(actions);

            Version = version;
            RawJson = rawJson;
            Entities = entities;
            Actions = actions;
            VictoryTarget = victoryTarget;
            Templates = templates ?? new Dictionary<string, IReadOnlyDictionary<string, Dictionary<string, JsonElement>>>();
            Collisions = collisions ?? new Dictionary<string, string>();
        }
    }
}
