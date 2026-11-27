using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

namespace SpaceBattle.Lib
{
    public sealed class CommandMessageFactory : ICommandMessageDecoding
    {
        public bool TryDecode(
            ReadOnlySpan<byte> utf8Json,
            [MaybeNullWhen(false)] out CommandDescription message,
            out string errorCode)
        {
            CommandDescription? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<CommandDescription>(utf8Json);
            }
            catch (JsonException)
            {
                message = null;
                errorCode = "invalid_json";
                return false;
            }

            if (parsed == null
                || string.IsNullOrWhiteSpace(parsed.GameId)
                || string.IsNullOrWhiteSpace(parsed.Type))
            {
                message = null;
                errorCode = "invalid_message";
                return false;
            }

            message = parsed;
            errorCode = string.Empty;
            return true;
        }

        public ReadOnlyMemory<byte> EncodeError(string code, string gameId, string message)
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                writer.WriteString("kind", "error");
                writer.WriteString("code", code);
                writer.WriteString("game_id", gameId);
                writer.WriteString("message", message);
                writer.WriteEndObject();
            }

            return buffer.WrittenMemory;
        }

        public static CommandArguments? DecodeArguments(
            CommandDescription description,
            SnapshotValueCodecs codecs)
        {
            ArgumentNullException.ThrowIfNull(description);
            ArgumentNullException.ThrowIfNull(codecs);

            if (description.Args == null || description.Args.Count == 0)
            {
                return null;
            }

            var decoded = new Dictionary<string, object>(description.Args.Count, StringComparer.Ordinal);
            foreach (var pair in description.Args)
            {
                decoded[pair.Key] = codecs.Read(pair.Value);
            }

            return new CommandArguments(decoded);
        }

        public static ReadOnlyMemory<byte> EncodeTickState(
            string gameId,
            long step,
            string status,
            string? winner,
            IReadOnlyGameObjectRepository repository,
            IGameObjectIdSource idSource,
            SnapshotValueCodecs codecs)
        {
            ArgumentNullException.ThrowIfNull(repository);
            ArgumentNullException.ThrowIfNull(idSource);
            ArgumentNullException.ThrowIfNull(codecs);

            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                writer.WriteString("kind", "state");
                writer.WriteString("game_id", gameId);
                writer.WriteNumber("step", step);
                writer.WriteString("status", status);
                if (winner == null)
                {
                    writer.WriteNull("winner");
                }
                else
                {
                    writer.WriteString("winner", winner);
                }

                writer.WriteStartObject("objects");
                foreach (var id in idSource.Ids)
                {
                    var item = repository.Get(id);
                    writer.WriteStartObject(id.ToString(CultureInfo.InvariantCulture));
                    foreach (var property in item)
                    {
                        if (codecs.TryWrite(property.Value, out var encoded))
                        {
                            writer.WriteStartObject(property.Key);
                            writer.WritePropertyName("type");
                            encoded["type"].WriteTo(writer);
                            writer.WritePropertyName("value");
                            encoded["value"].WriteTo(writer);
                            writer.WriteEndObject();
                        }
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            return buffer.WrittenMemory;
        }

        public static ReadOnlyMemory<byte> EncodeGameOver(
            string gameId,
            long step,
            string winner,
            IReadOnlyDictionary<string, int> aliveShips)
        {
            ArgumentNullException.ThrowIfNull(aliveShips);

            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                writer.WriteString("kind", "game_over");
                writer.WriteString("game_id", gameId);
                writer.WriteNumber("step", step);
                writer.WriteString("status", "finished");
                writer.WriteString("winner", winner);
                writer.WriteStartObject("alive_ships");
                foreach (var pair in aliveShips)
                {
                    writer.WriteNumber(pair.Key, pair.Value);
                }

                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            return buffer.WrittenMemory;
        }
    }
}
