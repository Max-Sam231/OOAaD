using App;

namespace SpaceBattle.Lib
{
    public interface IShootable
    {
        ICommand ProjectileCommand { get; }
        ICommandReceiver Receiver { get; }
    }
}
