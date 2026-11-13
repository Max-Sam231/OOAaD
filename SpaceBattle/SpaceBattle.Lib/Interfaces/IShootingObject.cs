using App;

namespace SpaceBattle.Lib
{
    public interface IShootingObject
    {
        ICommand ProjectileCommand { get; }
        ICommandReceiver Receiver { get; }
    }
}
