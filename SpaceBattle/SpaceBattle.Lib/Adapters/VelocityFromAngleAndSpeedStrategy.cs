using SpaceBattle.Lib;

[assembly: Dependency(typeof(IMovingObject), "Velocity", typeof(VelocityFromAngleAndSpeedStrategy), "Resolve")]

namespace SpaceBattle.Lib
{
    public static class VelocityFromAngleAndSpeedStrategy
    {
        public static object Resolve(object[] args)
        {
            var gameItem = (IDictionary<string, object>)args[0];
            var speed = (int)gameItem["Speed"];
            var angle = (Angle)gameItem["Angle"];
            double radians = angle;

            return new Vector(
                (int)Math.Round(speed * Math.Cos(radians), MidpointRounding.AwayFromZero),
                (int)Math.Round(speed * Math.Sin(radians), MidpointRounding.AwayFromZero));
        }
    }
}
