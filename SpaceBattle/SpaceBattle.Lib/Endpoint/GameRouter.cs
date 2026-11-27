using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace SpaceBattle.Lib
{
    public sealed class GameRouter : IGameRouter
    {
        private readonly ConcurrentDictionary<string, IGameEndpointRoute> _routes =
            new ConcurrentDictionary<string, IGameEndpointRoute>(StringComparer.Ordinal);

        public MessageRoutingStatus Route(CommandDescription message)
        {
            ArgumentNullException.ThrowIfNull(message);

            if (!_routes.TryGetValue(message.GameId, out var route))
            {
                return MessageRoutingStatus.GameNotFound;
            }

            return route.TryRoute(message) ? MessageRoutingStatus.Accepted : MessageRoutingStatus.QueueFull;
        }

        public void Register(string gameId, IGameEndpointRoute route)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
            ArgumentNullException.ThrowIfNull(route);

            _routes[gameId] = route;
        }

        public bool Unregister(string gameId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameId);

            return _routes.TryRemove(gameId, out _);
        }

        public bool TryGetRoute(string gameId, [MaybeNullWhen(false)] out IGameEndpointRoute route)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameId);

            return _routes.TryGetValue(gameId, out route);
        }
    }
}
