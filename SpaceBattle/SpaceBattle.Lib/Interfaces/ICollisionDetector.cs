namespace SpaceBattle.Lib
{
    public interface ICollisionDetector
    {
        bool DetectCollision(ICollidable a, ICollidable b);
    }
}
