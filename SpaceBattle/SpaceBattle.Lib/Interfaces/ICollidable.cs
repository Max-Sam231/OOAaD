using SpaceBattle.Lib;

namespace SpaceBattle.Lib
{
    public interface ICollidable
    {
        Vector Position { get; }
        Vector Velocity { get; }
    }
}
