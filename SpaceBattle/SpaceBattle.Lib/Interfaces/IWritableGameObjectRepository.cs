using System.Collections.Generic;

namespace SpaceBattle.Lib
{
    public interface IWritableGameObjectRepository
    {
        void Set(int id, IDictionary<string, object> gameObject);
        void Remove(int id);
    }
}
