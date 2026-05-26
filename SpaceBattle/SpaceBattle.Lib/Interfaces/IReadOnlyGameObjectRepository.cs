using System.Collections.Generic;

namespace SpaceBattle.Lib
{
    public interface IReadOnlyGameObjectRepository
    {
        IDictionary<string, object> Get(int id);
    }
}
