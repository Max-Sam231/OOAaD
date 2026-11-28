using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpaceBattle.Lib
{
    public class GameSnapshot
    {
        [JsonPropertyName("rules")]
        public string Rules { get; init; } = "";

        [JsonPropertyName("command_limit")]
        public int CommandLimit { get; init; }

        [JsonPropertyName("items")]
        public Dictionary<int, Dictionary<string, Dictionary<string, JsonElement>>> Items { get; init; } =
            new Dictionary<int, Dictionary<string, Dictionary<string, JsonElement>>>();

        [JsonPropertyName("commands")]
        public List<CommandDescription> Commands { get; init; } = new List<CommandDescription>();
    }
}
