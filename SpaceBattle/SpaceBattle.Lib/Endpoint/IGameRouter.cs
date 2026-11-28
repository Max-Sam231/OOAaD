using System.Diagnostics.CodeAnalysis;

namespace SpaceBattle.Lib
{
    public interface IGameRouter
    {
        MessageRoutingStatus Route(CommandDescription message);
        void Register(string gameId, IGameEndpointRoute route);
        bool Unregister(string gameId);
        bool TryGetRoute(string gameId, [MaybeNullWhen(false)] out IGameEndpointRoute route);
    }
}
