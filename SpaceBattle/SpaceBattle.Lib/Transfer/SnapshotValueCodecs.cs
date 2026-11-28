using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace SpaceBattle.Lib
{
    public class SnapshotValueCodecs
    {
        private readonly Dictionary<string, Func<object, JsonElement>> _writers = new Dictionary<string, Func<object, JsonElement>>();
        private readonly Dictionary<string, Func<JsonElement, object>> _readers = new Dictionary<string, Func<JsonElement, object>>();

        public void Add(Type type, Func<object, JsonElement> write, Func<JsonElement, object> read)
        {
            _writers[type.FullName!] = write;
            _readers[type.FullName!] = read;
        }

        public bool TryWrite(object? value, [MaybeNullWhen(false)] out Dictionary<string, JsonElement> written)
        {
            var typeName = value?.GetType().FullName ?? "";

            if (!_writers.TryGetValue(typeName, out var write))
            {
                written = null;

                return false;
            }

            written = new Dictionary<string, JsonElement>
            {
                ["type"] = JsonSerializer.SerializeToElement(typeName),
                ["value"] = write(value!),
            };

            return true;
        }

        public object Read(IReadOnlyDictionary<string, JsonElement> written)
        {
            if (!written.TryGetValue("type", out var type)
                || !written.TryGetValue("value", out var value)
                || !_readers.TryGetValue(type.ToString(), out var read))
            {
                throw new InvalidOperationException("The snapshot contains a value that cannot be read.");
            }

            return read(value);
        }
    }
}
