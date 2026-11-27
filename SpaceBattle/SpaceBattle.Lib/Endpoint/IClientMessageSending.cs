namespace SpaceBattle.Lib
{
    public interface IClientMessageSending
    {
        bool TrySend(ReadOnlyMemory<byte> payload);
        void Complete();
    }
}
