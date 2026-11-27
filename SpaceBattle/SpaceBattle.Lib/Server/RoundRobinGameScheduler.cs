using System.Diagnostics.CodeAnalysis;
using App;

namespace SpaceBattle.Lib
{
    public class RoundRobinGameScheduler : IGameScheduler
    {
        private readonly Queue<ICommand> _order = new Queue<ICommand>();
        private readonly HashSet<ICommand> _ready = new HashSet<ICommand>();

        public bool HasReady => _order.Count > 0;

        public void MakeReady(ICommand game)
        {
            if (_ready.Add(game))
            {
                _order.Enqueue(game);
            }
        }

        public bool TryTakeReady([MaybeNullWhen(false)] out ICommand game)
        {
            if (!_order.TryDequeue(out game))
            {
                return false;
            }

            _ready.Remove(game);

            return true;
        }
    }
}
