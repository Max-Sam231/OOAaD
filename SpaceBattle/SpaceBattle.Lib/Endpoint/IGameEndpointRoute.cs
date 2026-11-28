namespace SpaceBattle.Lib
{
    public interface IGameEndpointRoute
    {
        bool TryRoute(CommandDescription message);
    }
}
