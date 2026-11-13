namespace SpaceBattle.Lib
{
    public class EnumerableGameObjectRepository : IReadOnlyGameObjectRepository, IWritableGameObjectRepository, IGameObjectIdSource
    {
        private readonly Dictionary<int, IDictionary<string, object>> _items = new Dictionary<int, IDictionary<string, object>>();

        public IEnumerable<int> Ids => _items.Keys;

        public IDictionary<string, object> Get(int id)
        {
            return _items[id];
        }

        public void Set(int id, IDictionary<string, object> gameObject)
        {
            _items[id] = gameObject;
        }

        public void Remove(int id)
        {
            if (!_items.Remove(id))
            {
                throw new KeyNotFoundException();
            }
        }
    }
}
