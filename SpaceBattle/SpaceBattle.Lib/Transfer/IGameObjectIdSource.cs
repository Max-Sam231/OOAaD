namespace SpaceBattle.Lib
{
    public interface IGameObjectIdSource
    {
        IEnumerable<int> Ids { get; }
    }
}
