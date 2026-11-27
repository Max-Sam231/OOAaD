using System.Threading.Channels;
using SpaceBattle.Lib;

namespace SpaceBattle.Server
{
    public sealed class ChannelClientMessageSending : IClientMessageSending
    {
        private readonly Channel<ReadOnlyMemory<byte>> _channel;

        public ChannelClientMessageSending(Channel<ReadOnlyMemory<byte>> channel)
        {
            ArgumentNullException.ThrowIfNull(channel);
            _channel = channel;
        }

        public ChannelReader<ReadOnlyMemory<byte>> Reader => _channel.Reader;

        public bool TrySend(ReadOnlyMemory<byte> payload)
        {
            return _channel.Writer.TryWrite(payload);
        }

        public void Complete()
        {
            _channel.Writer.TryComplete();
        }
    }
}
