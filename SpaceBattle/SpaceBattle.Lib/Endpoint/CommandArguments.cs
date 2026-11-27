namespace SpaceBattle.Lib
{
    public sealed class CommandArguments
    {
        private readonly IReadOnlyDictionary<string, object> _values;

        public CommandArguments(IReadOnlyDictionary<string, object> values)
        {
            ArgumentNullException.ThrowIfNull(values);

            _values = values;
        }

        public int Count => _values.Count;

        public bool Contains(string key)
        {
            ArgumentNullException.ThrowIfNull(key);

            return _values.ContainsKey(key);
        }

        public T Get<T>(string key)
        {
            ArgumentNullException.ThrowIfNull(key);

            if (!_values.TryGetValue(key, out var value))
            {
                throw new KeyNotFoundException($"Command argument '{key}' was not found.");
            }

            return (T)value;
        }
    }
}
