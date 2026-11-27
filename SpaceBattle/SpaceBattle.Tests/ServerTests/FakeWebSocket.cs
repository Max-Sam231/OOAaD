using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace SpaceBattle.Test
{
    public sealed class FakeWebSocket : WebSocket
    {
        private readonly Queue<Func<Memory<byte>, CancellationToken, ValueTask<ValueWebSocketReceiveResult>>> _receives = new();
        private readonly ConcurrentQueue<string> _sentFrames = new();
        private WebSocketState _state = WebSocketState.Open;
        private WebSocketCloseStatus? _closeStatus;
        private string? _closeStatusDescription;

        public Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask>? OnSendAsync { get; set; }

        public WebSocketCloseStatus? CloseOutputStatus { get; private set; }

        public string? CloseOutputDescription { get; private set; }

        public IReadOnlyList<string> SentFrames => _sentFrames.ToArray();

        public override WebSocketCloseStatus? CloseStatus => _closeStatus;

        public override string? CloseStatusDescription => _closeStatusDescription;

        public override WebSocketState State => _state;

        public override string? SubProtocol => null;

        public void SetState(WebSocketState state)
        {
            _state = state;
        }

        public void EnqueueText(string text, bool endOfMessage = true)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            EnqueueFrame(bytes, WebSocketMessageType.Text, endOfMessage);
        }

        public void EnqueueFrame(byte[] payload, WebSocketMessageType messageType, bool endOfMessage)
        {
            _receives.Enqueue((buffer, _) =>
            {
                payload.AsSpan().CopyTo(buffer.Span);
                return ValueTask.FromResult(new ValueWebSocketReceiveResult(payload.Length, messageType, endOfMessage));
            });
        }

        public void EnqueueClose()
        {
            _receives.Enqueue((_, _) =>
            {
                _state = WebSocketState.CloseReceived;
                return ValueTask.FromResult(new ValueWebSocketReceiveResult(0, WebSocketMessageType.Close, true));
            });
        }

        public void EnqueueReceiveStep(Func<Memory<byte>, CancellationToken, ValueTask<ValueWebSocketReceiveResult>> step)
        {
            _receives.Enqueue(step);
        }

        public override ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken)
        {
            if (_receives.Count == 0)
            {
                _state = WebSocketState.CloseReceived;
                return ValueTask.FromResult(new ValueWebSocketReceiveResult(0, WebSocketMessageType.Close, true));
            }

            return _receives.Dequeue()(buffer, cancellationToken);
        }

        public override Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public override async ValueTask SendAsync(
            ReadOnlyMemory<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
        {
            if (OnSendAsync != null)
            {
                await OnSendAsync(buffer, cancellationToken);
                return;
            }

            _sentFrames.Enqueue(Encoding.UTF8.GetString(buffer.Span));
        }

        public override Task SendAsync(
            ArraySegment<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public override Task CloseOutputAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            CloseOutputStatus = closeStatus;
            CloseOutputDescription = statusDescription;
            _closeStatus = closeStatus;
            _closeStatusDescription = statusDescription;
            _state = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public override Task CloseAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            return CloseOutputAsync(closeStatus, statusDescription, cancellationToken);
        }

        public override void Abort()
        {
            _state = WebSocketState.Aborted;
        }

        public override void Dispose()
        {
        }
    }
}
