using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace SpaceBattle.Lib
{
    public class TransactionalGameItem : IDictionary<string, object>
    {
        private readonly IDictionary<string, object> _item;
        private readonly GameTransactions _transactions;

        public TransactionalGameItem(IDictionary<string, object> item, GameTransactions transactions)
        {
            _item = item ?? throw new ArgumentNullException(nameof(item));
            _transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
        }

        public ICollection<string> Keys => View().Keys;

        public ICollection<object> Values => View().Values;

        public int Count => View().Count;

        public bool IsReadOnly => false;

        public object this[string key]
        {
            get => _transactions.TryRead(_item, key, out var value) ? value : _item[key];
            set
            {
                if (!_transactions.TryWrite(_item, key, value))
                {
                    _item[key] = value;
                }
            }
        }

        public void Add(string key, object value)
        {
            if (ContainsKey(key))
            {
                throw new ArgumentException($"The item already has the key {key}.", nameof(key));
            }

            this[key] = value;
        }

        public void Add(KeyValuePair<string, object> item)
        {
            Add(item.Key, item.Value);
        }

        public bool ContainsKey(string key)
        {
            return View().ContainsKey(key);
        }

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out object value)
        {
            return View().TryGetValue(key, out value);
        }

        public bool Contains(KeyValuePair<string, object> item)
        {
            return View().Contains(item);
        }

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<string, object>>)View()).CopyTo(array, arrayIndex);
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return View().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool Remove(string key)
        {
            RequireNoTransaction();

            return _item.Remove(key);
        }

        public bool Remove(KeyValuePair<string, object> item)
        {
            RequireNoTransaction();

            return _item.Remove(item);
        }

        public void Clear()
        {
            RequireNoTransaction();
            _item.Clear();
        }

        private Dictionary<string, object> View()
        {
            var view = new Dictionary<string, object>(_item);

            foreach (var write in _transactions.Pending(_item))
            {
                view[write.Key] = write.Value;
            }

            return view;
        }

        private void RequireNoTransaction()
        {
            if (_transactions.IsActive)
            {
                throw new NotSupportedException("Keys cannot be removed from a game item while a transaction is open.");
            }
        }
    }
}
