using System.Diagnostics.CodeAnalysis;

namespace SpaceBattle.Lib
{
    public class GameTransactions
    {
        private readonly Stack<Dictionary<(IDictionary<string, object> Item, string Key), object>> _journals =
            new Stack<Dictionary<(IDictionary<string, object> Item, string Key), object>>();

        public bool IsActive => _journals.Count > 0;

        public void Begin()
        {
            _journals.Push(new Dictionary<(IDictionary<string, object> Item, string Key), object>());
        }

        public void Commit()
        {
            var journal = Close();

            if (IsActive)
            {
                var outer = _journals.Peek();

                foreach (var write in journal)
                {
                    outer[write.Key] = write.Value;
                }
            }
            else
            {
                foreach (var write in journal)
                {
                    write.Key.Item[write.Key.Key] = write.Value;
                }
            }
        }

        public void Rollback()
        {
            Close();
        }

        public bool TryWrite(IDictionary<string, object> item, string key, object value)
        {
            if (!IsActive)
            {
                return false;
            }

            _journals.Peek()[(item, key)] = value;

            return true;
        }

        public bool TryRead(IDictionary<string, object> item, string key, [MaybeNullWhen(false)] out object value)
        {
            foreach (var journal in _journals)
            {
                if (journal.TryGetValue((item, key), out value))
                {
                    return true;
                }
            }

            value = null;

            return false;
        }

        public IEnumerable<KeyValuePair<string, object>> Pending(IDictionary<string, object> item)
        {
            return _journals
                .Reverse()
                .SelectMany(journal => journal)
                .Where(write => ReferenceEquals(write.Key.Item, item))
                .Select(write => KeyValuePair.Create(write.Key.Key, write.Value));
        }

        private Dictionary<(IDictionary<string, object> Item, string Key), object> Close()
        {
            if (!IsActive)
            {
                throw new InvalidOperationException("No transaction is open.");
            }

            return _journals.Pop();
        }
    }
}
