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
    }
}
