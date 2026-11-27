using System.Collections.Concurrent;

namespace SpaceBattle.Lib
{
    public sealed class GameConnectionRegistry : IGameConnectionRegistry
    {
        private readonly object _sync = new object();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, IClientMessageSending>> _games =
            new ConcurrentDictionary<string, ConcurrentDictionary<string, IClientMessageSending>>(StringComparer.Ordinal);

        public void Subscribe(string gameId, string connectionId, IClientMessageSending sender)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
            ArgumentNullException.ThrowIfNull(sender);

            lock (_sync)
            {
                var connections = _games.GetOrAdd(
                    gameId,
                    _ => new ConcurrentDictionary<string, IClientMessageSending>(StringComparer.Ordinal));
                connections[connectionId] = sender;
            }
        }

        public void Unsubscribe(string gameId, string connectionId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

            lock (_sync)
            {
                if (!_games.TryGetValue(gameId, out var connections))
                {
                    return;
                }

                connections.TryRemove(connectionId, out _);

                if (connections.IsEmpty)
                {
                    _games.TryRemove(gameId, out _);
                }
            }
        }

        public void Publish(string gameId, ReadOnlyMemory<byte> payload)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameId);

            if (!_games.TryGetValue(gameId, out var connections))
            {
                return;
            }

            foreach (var pair in connections)
            {
                pair.Value.TrySend(payload);
            }
        }

        public int GetConnectionCount(string gameId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameId);

            return _games.TryGetValue(gameId, out var connections) ? connections.Count : 0;
        }
    }
}
