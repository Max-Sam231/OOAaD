namespace SpaceBattle.Lib
{
    public interface IRotatable
    {
        Angle Angle { get; set; }
        Angle AngularVelocity { get; }
    }
}
