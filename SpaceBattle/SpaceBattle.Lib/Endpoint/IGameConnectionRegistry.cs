namespace SpaceBattle.Lib
{
    public interface IGameConnectionRegistry
    {
        void Subscribe(string gameId, string connectionId, IClientMessageSending sender);
        void Unsubscribe(string gameId, string connectionId);
        void Publish(string gameId, ReadOnlyMemory<byte> payload);
        int GetConnectionCount(string gameId);
    }
}
