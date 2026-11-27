using System.Diagnostics.CodeAnalysis;

namespace SpaceBattle.Lib
{
    public interface IGameReceiverRegistry
    {
        void Set(string gameId, IGameCommandSending target);
        bool RemoveIfSame(string gameId, IGameCommandSending expected);
        bool TryGet(string gameId, [MaybeNullWhen(false)] out IGameCommandSending target);
        IReadOnlyCollection<string> GameIds { get; }
    }
}
