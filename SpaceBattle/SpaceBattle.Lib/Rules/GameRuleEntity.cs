using System.Text.Json;

namespace SpaceBattle.Lib
{
    public sealed class GameRuleEntity
    {
        public int Id { get; }

        public IReadOnlyDictionary<string, Dictionary<string, JsonElement>> Properties { get; }

        public GameRuleEntity(int id, IReadOnlyDictionary<string, Dictionary<string, JsonElement>> properties)
        {
            ArgumentNullException.ThrowIfNull(properties);

            Id = id;
            Properties = properties;
        }
    }
}
