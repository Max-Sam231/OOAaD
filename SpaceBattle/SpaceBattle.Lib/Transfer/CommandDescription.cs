using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpaceBattle.Lib
{
    public class CommandDescription
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = "";

        [JsonPropertyName("type")]
        public string Type { get; init; } = "";

        [JsonPropertyName("game_item_id")]
        public int GameItemId { get; init; }

        [JsonPropertyName("game_id")]
        public string GameId { get; init; } = "";

        [JsonPropertyName("player_id")]
        public string PlayerId { get; init; } = "";

        [JsonPropertyName("step")]
        public long Step { get; init; }

        [JsonPropertyName("args")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, Dictionary<string, JsonElement>>? Args { get; init; }
    }
}
