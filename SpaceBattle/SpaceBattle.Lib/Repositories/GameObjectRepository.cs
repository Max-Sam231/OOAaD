using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SpaceBattle.Lib
{
    public class GameObjectRepository : IReadOnlyGameObjectRepository, IWritableGameObjectRepository
    {
        private readonly ConcurrentDictionary<int, IDictionary<string, object>> _repository;

        public GameObjectRepository()
        {
            _repository = new ConcurrentDictionary<int, IDictionary<string, object>>();
        }

        public IDictionary<string, object> Get(int id)
        {
            if (_repository.TryGetValue(id, out var gameObject))
            {
                return gameObject;
            }
            throw new KeyNotFoundException();
        }

        public void Set(int id, IDictionary<string, object> gameObject)
        {
            _repository[id] = gameObject;
        }

        public void Remove(int id)
        {
            if (!_repository.TryRemove(id, out _))
            {
                throw new KeyNotFoundException();
            }
        }
    }
}
