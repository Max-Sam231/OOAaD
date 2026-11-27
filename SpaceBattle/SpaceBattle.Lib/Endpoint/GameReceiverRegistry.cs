using System.Diagnostics.CodeAnalysis;

namespace SpaceBattle.Lib
{
    public sealed class GameReceiverRegistry : IGameReceiverRegistry
    {
        private readonly Dictionary<string, IGameCommandSending> _targets =
            new Dictionary<string, IGameCommandSending>(StringComparer.Ordinal);

        public IReadOnlyCollection<string> GameIds => _targets.Keys;

        public void Set(string gameId, IGameCommandSending target)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
            ArgumentNullException.ThrowIfNull(target);

            _targets[gameId] = target;
        }

        public bool RemoveIfSame(string gameId, IGameCommandSending expected)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
            ArgumentNullException.ThrowIfNull(expected);

            if (!_targets.TryGetValue(gameId, out var current) || !ReferenceEquals(current, expected))
            {
                return false;
            }

            _targets.Remove(gameId);
            return true;
        }

        public bool TryGet(string gameId, [MaybeNullWhen(false)] out IGameCommandSending target)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameId);

            return _targets.TryGetValue(gameId, out target);
        }
    }
}
