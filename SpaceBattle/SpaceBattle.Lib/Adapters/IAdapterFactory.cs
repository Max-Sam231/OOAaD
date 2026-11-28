namespace SpaceBattle.Lib
{
    public interface IAdapterFactory
    {
        object Create(IDictionary<string, object> gameItem);
    }
}
