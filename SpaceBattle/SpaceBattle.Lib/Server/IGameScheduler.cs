using System.Diagnostics.CodeAnalysis;
using App;

namespace SpaceBattle.Lib
{
    public interface IGameScheduler
    {
        bool HasReady { get; }

        void MakeReady(ICommand game);

        bool TryTakeReady([MaybeNullWhen(false)] out ICommand game);
    }
}
