using SpaceBattle.Lib;

namespace SpaceBattle.Lib
{
    public interface ICollidingObject
    {
        Vector Position { get; }
        Vector Velocity { get; }
    }
}
