using System.Text;
using System.Text.Json;
using SpaceBattle.Lib;

namespace SpaceBattle.Test
{
    public class CommandMessageFactoryTests
    {
        private readonly CommandMessageFactory _factory = new CommandMessageFactory();

        [Theory]
        [InlineData("")]
        [InlineData("{bad")]
        [InlineData("[1,2")]
        public void TryDecode_InvalidJson_ReturnsInvalidJsonError(string json)
        {
            var result = _factory.TryDecode(Encoding.UTF8.GetBytes(json), out var message, out var errorCode);

            Assert.False(result);
            Assert.Null(message);
            Assert.Equal("invalid_json", errorCode);
        }

        [Theory]
        [InlineData("null")]
        [InlineData("{}")]
        [InlineData("{\"game_id\":\"g1\"}")]
        [InlineData("{\"game_id\":\"g1\",\"type\":\"\"}")]
        [InlineData("{\"game_id\":\"   \",\"type\":\"Commands.Move\"}")]
        [InlineData("{\"game_id\":\"g1\",\"type\":\"   \"}")]
        public void TryDecode_MissingRequiredFields_ReturnsInvalidMessageError(string json)
        {
            var result = _factory.TryDecode(Encoding.UTF8.GetBytes(json), out var message, out var errorCode);

            Assert.False(result);
            Assert.Null(message);
            Assert.Equal("invalid_message", errorCode);
        }

        [Fact]
        public void TryDecode_ValidJsonWithoutArgs_PopulatesDefaults()
        {
            var json = "{\"game_id\":\"game-1\",\"type\":\"Commands.Move\",\"game_item_id\":7}";

            var result = _factory.TryDecode(Encoding.UTF8.GetBytes(json), out var message, out var errorCode);

            Assert.True(result);
            Assert.Equal(string.Empty, errorCode);
            Assert.NotNull(message);
            Assert.Equal(string.Empty, message.Id);
            Assert.Equal("game-1", message.GameId);
            Assert.Equal(string.Empty, message.PlayerId);
            Assert.Equal("Commands.Move", message.Type);
            Assert.Equal(7, message.GameItemId);
            Assert.Equal(0L, message.Step);
            Assert.Null(message.Args);
        }

        [Fact]
        public void TryDecodeAndDecodeArguments_WithTypedArgs_DecodesAllValuesAndExposesCommandArguments()
        {
            var json =
                "{\"id\":\"cmd-42\",\"game_id\":\"game-1\",\"player_id\":\"player-2\",\"type\":\"Macro.Move\",\"game_item_id\":2,\"step\":5," +
                "\"args\":{" +
                "\"speed\":{\"type\":\"System.Int32\",\"value\":3}," +
                "\"label\":{\"type\":\"System.String\",\"value\":\"alpha\"}," +
                "\"offset\":{\"type\":\"SpaceBattle.Lib.Vector\",\"value\":[4,-2]}," +
                "\"heading\":{\"type\":\"SpaceBattle.Lib.Angle\",\"value\":[3,8]}" +
                "}}";

            var decodedOk = _factory.TryDecode(Encoding.UTF8.GetBytes(json), out var message, out var errorCode);
            Assert.True(decodedOk);
            Assert.Equal(string.Empty, errorCode);
            Assert.NotNull(message);
            Assert.Equal("cmd-42", message.Id);
            Assert.Equal("player-2", message.PlayerId);
            Assert.Equal(5L, message.Step);

            var codecs = CreateCodecs();
            var args = CommandMessageFactory.DecodeArguments(message, codecs);

            Assert.NotNull(args);
            Assert.Equal(4, args.Count);
            Assert.True(args.Contains("speed"));
            Assert.False(args.Contains("missing"));
            Assert.Equal(3, args.Get<int>("speed"));
            Assert.Equal("alpha", args.Get<string>("label"));
            Assert.Equal(new Vector(4, -2), args.Get<Vector>("offset"));
            Assert.Equal(new Angle(3, 8), args.Get<Angle>("heading"));
            Assert.Throws<KeyNotFoundException>(() => args.Get<int>("missing"));
        }

        [Fact]
        public void CommandArguments_NullArguments_ThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new CommandArguments(null!));

            var args = new CommandArguments(new Dictionary<string, object>());
            Assert.Throws<ArgumentNullException>(() => args.Contains(null!));
            Assert.Throws<ArgumentNullException>(() => args.Get<int>(null!));
        }

        [Fact]
        public void DecodeArguments_NullOrEmptyArgs_ReturnsNull()
        {
            var codecs = CreateCodecs();

            Assert.Null(CommandMessageFactory.DecodeArguments(
                new CommandDescription { GameId = "g1", Type = "Commands.Move", Args = null },
                codecs));
            Assert.Null(CommandMessageFactory.DecodeArguments(
                new CommandDescription
                {
                    GameId = "g1",
                    Type = "Commands.Move",
                    Args = new Dictionary<string, Dictionary<string, JsonElement>>(),
                },
                codecs));
            Assert.Throws<ArgumentNullException>(() => CommandMessageFactory.DecodeArguments(null!, codecs));
            Assert.Throws<ArgumentNullException>(() => CommandMessageFactory.DecodeArguments(new CommandDescription(), null!));
        }

        [Fact]
        public void EncodeError_ProducesExpectedSchema()
        {
            var payload = _factory.EncodeError("busy", "game-1", "ingress_paused");
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            Assert.Equal("error", root.GetProperty("kind").GetString());
            Assert.Equal("busy", root.GetProperty("code").GetString());
            Assert.Equal("game-1", root.GetProperty("game_id").GetString());
            Assert.Equal("ingress_paused", root.GetProperty("message").GetString());
        }

        [Fact]
        public void EncodeTickState_WithNullAndNonNullWinner_SerializesKnownPropertiesAndSkipsUnknown()
        {
            var repo = new EnumerableGameObjectRepository();
            repo.Set(1, new Dictionary<string, object>
            {
                ["Position"] = new Vector(10, 5),
                ["Angle"] = new Angle(1, 8),
                ["RuntimeOnly"] = new object(),
            });
            var codecs = CreateCodecs();

            var runningPayload = CommandMessageFactory.EncodeTickState(
                "game-1",
                6,
                "running",
                null,
                repo,
                repo,
                codecs);
            using var runningDoc = JsonDocument.Parse(runningPayload);
            var runningRoot = runningDoc.RootElement;

            Assert.Equal("state", runningRoot.GetProperty("kind").GetString());
            Assert.Equal("game-1", runningRoot.GetProperty("game_id").GetString());
            Assert.Equal(6L, runningRoot.GetProperty("step").GetInt64());
            Assert.Equal("running", runningRoot.GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Null, runningRoot.GetProperty("winner").ValueKind);

            var itemJson = runningRoot.GetProperty("objects").GetProperty("1");
            Assert.Equal("SpaceBattle.Lib.Vector", itemJson.GetProperty("Position").GetProperty("type").GetString());
            Assert.Equal(new[] { 10, 5 }, itemJson.GetProperty("Position").GetProperty("value").Deserialize<int[]>());
            Assert.Equal("SpaceBattle.Lib.Angle", itemJson.GetProperty("Angle").GetProperty("type").GetString());
            Assert.Equal(new[] { 1, 8 }, itemJson.GetProperty("Angle").GetProperty("value").Deserialize<int[]>());
            Assert.False(itemJson.TryGetProperty("RuntimeOnly", out _));

            var finishedPayload = CommandMessageFactory.EncodeTickState(
                "game-1",
                12,
                "finished",
                "player-2",
                repo,
                repo,
                codecs);
            using var finishedDoc = JsonDocument.Parse(finishedPayload);
            Assert.Equal("player-2", finishedDoc.RootElement.GetProperty("winner").GetString());

            Assert.Throws<ArgumentNullException>(() =>
                CommandMessageFactory.EncodeTickState("g1", 0, "running", null, null!, repo, codecs));
            Assert.Throws<ArgumentNullException>(() =>
                CommandMessageFactory.EncodeTickState("g1", 0, "running", null, repo, null!, codecs));
            Assert.Throws<ArgumentNullException>(() =>
                CommandMessageFactory.EncodeTickState("g1", 0, "running", null, repo, repo, null!));
        }

        [Fact]
        public void EncodeGameOver_ProducesExpectedSchemaWithMultiplePlayers()
        {
            var alive = new Dictionary<string, int>
            {
                ["player-1"] = 0,
                ["player-2"] = 2,
            };

            var payload = CommandMessageFactory.EncodeGameOver("game-1", 18, "player-2", alive);
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            Assert.Equal("game_over", root.GetProperty("kind").GetString());
            Assert.Equal("game-1", root.GetProperty("game_id").GetString());
            Assert.Equal(18L, root.GetProperty("step").GetInt64());
            Assert.Equal("finished", root.GetProperty("status").GetString());
            Assert.Equal("player-2", root.GetProperty("winner").GetString());
            Assert.Equal(0, root.GetProperty("alive_ships").GetProperty("player-1").GetInt32());
            Assert.Equal(2, root.GetProperty("alive_ships").GetProperty("player-2").GetInt32());

            Assert.Throws<ArgumentNullException>(() =>
                CommandMessageFactory.EncodeGameOver("game-1", 18, "player-2", null!));
        }

        private static SnapshotValueCodecs CreateCodecs()
        {
            var codecs = new SnapshotValueCodecs();
            codecs.Add(
                typeof(int),
                value => JsonSerializer.SerializeToElement((int)value),
                element => element.GetInt32());
            codecs.Add(
                typeof(string),
                value => JsonSerializer.SerializeToElement((string)value),
                element => element.GetString()!);
            codecs.Add(
                typeof(Vector),
                value =>
                {
                    var vector = (Vector)value;
                    var coordinates = new int[vector.Dimension];
                    for (var i = 0; i < coordinates.Length; i++)
                    {
                        coordinates[i] = vector[i];
                    }

                    return JsonSerializer.SerializeToElement(coordinates);
                },
                element => new Vector(element.Deserialize<int[]>()!));
            codecs.Add(
                typeof(Angle),
                value =>
                {
                    var angle = (Angle)value;
                    return JsonSerializer.SerializeToElement(new[] { angle.Numerator, angle.Denominator });
                },
                element =>
                {
                    var parts = element.Deserialize<int[]>()!;
                    return new Angle(parts[0], parts[1]);
                });

            return codecs;
        }
    }
}
