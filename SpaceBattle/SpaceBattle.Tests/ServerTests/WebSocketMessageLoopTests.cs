using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using App;
using NSubstitute;
using SpaceBattle.Lib;
using SpaceBattle.Server;

namespace SpaceBattle.Test
{
    public class WebSocketMessageLoopTests
    {
        private static ChannelClientMessageSending CreateSender(int capacity = 16)
        {
            var channel = Channel.CreateBounded<ReadOnlyMemory<byte>>(
                new BoundedChannelOptions(capacity)
                {
                    SingleReader = true,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.DropOldest,
                });
            return new ChannelClientMessageSending(channel);
        }

        [Fact]
        public void ServerOptions_HasExpectedDefaultsAndSupportsUpdates()
        {
            var options = new ServerOptions();
            Assert.Equal(2, options.WorkerThreadCount);
            Assert.Equal(64, options.OutboundChannelCapacity);
            Assert.Equal(65536, options.MaxWebSocketMessageBytes);
            Assert.Equal(TimeSpan.FromSeconds(5), options.GracefulStopTimeout);

            options.WorkerThreadCount = 4;
            options.OutboundChannelCapacity = 128;
            options.MaxWebSocketMessageBytes = 4096;
            options.GracefulStopTimeout = TimeSpan.FromSeconds(10);

            Assert.Equal(4, options.WorkerThreadCount);
            Assert.Equal(128, options.OutboundChannelCapacity);
            Assert.Equal(4096, options.MaxWebSocketMessageBytes);
            Assert.Equal(TimeSpan.FromSeconds(10), options.GracefulStopTimeout);
        }

        [Fact]
        public async Task WebSocketMessageLoop_ValidatesArguments()
        {
            var router = Substitute.For<IGameRouter>();
            var decoder = new CommandMessageFactory();
            var sender = CreateSender();

            Assert.Throws<ArgumentNullException>(() =>
                new WebSocketMessageLoop(null!, () => false, decoder, sender, 64));
            Assert.Throws<ArgumentNullException>(() =>
                new WebSocketMessageLoop(router, null!, decoder, sender, 64));
            Assert.Throws<ArgumentNullException>(() =>
                new WebSocketMessageLoop(router, () => false, null!, sender, 64));
            Assert.Throws<ArgumentNullException>(() =>
                new WebSocketMessageLoop(router, () => false, decoder, null!, 64));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new WebSocketMessageLoop(router, () => false, decoder, sender, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new WebSocketMessageLoop(router, () => false, decoder, sender, -5));

            var loop = new WebSocketMessageLoop(router, () => false, decoder, sender, 64);
            using var socket = new FakeWebSocket();

            await Assert.ThrowsAsync<ArgumentNullException>(() => loop.RunAsync(null!, () => { }, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentNullException>(() => loop.RunAsync(socket, null!, CancellationToken.None));
        }

        [Fact]
        public async Task SingleTextFrame_ValidPayload_RoutesAcceptedAndCallsOnDisconnected()
        {
            var router = Substitute.For<IGameRouter>();
            CommandDescription? routed = null;
            router.Route(Arg.Do<CommandDescription>(m => routed = m)).Returns(MessageRoutingStatus.Accepted);

            var sender = CreateSender();
            var loop = new WebSocketMessageLoop(router, () => false, new CommandMessageFactory(), sender, 1024);

            using var socket = new FakeWebSocket();
            socket.EnqueueText("""{"id":"1","game_id":"g1","type":"Commands.Move","game_item_id":7,"step":2}""");
            socket.EnqueueClose();

            var disconnected = false;
            await loop.RunAsync(socket, () => disconnected = true, CancellationToken.None);

            Assert.True(disconnected);
            Assert.NotNull(routed);
            Assert.Equal("g1", routed!.GameId);
            Assert.Equal("Commands.Move", routed.Type);
            Assert.Equal(7, routed.GameItemId);
            Assert.Equal(2L, routed.Step);
            Assert.Empty(socket.SentFrames);
            Assert.Equal(WebSocketCloseStatus.NormalClosure, socket.CloseOutputStatus);
        }

        [Fact]
        public async Task FragmentedTextFrame_ReassemblesChunksAndRoutesMessage()
        {
            var router = Substitute.For<IGameRouter>();
            CommandDescription? routed = null;
            router.Route(Arg.Do<CommandDescription>(m => routed = m)).Returns(MessageRoutingStatus.Accepted);

            var sender = CreateSender();
            var loop = new WebSocketMessageLoop(router, () => false, new CommandMessageFactory(), sender, 1024);

            using var socket = new FakeWebSocket();
            socket.EnqueueText("{\"game_id\":\"g2\",", endOfMessage: false);
            socket.EnqueueText("\"type\":\"Macro.Move\",\"game_item_id\":3,\"step\":1}", endOfMessage: true);
            socket.EnqueueClose();

            await loop.RunAsync(socket, () => { }, CancellationToken.None);

            Assert.NotNull(routed);
            Assert.Equal("g2", routed!.GameId);
            Assert.Equal("Macro.Move", routed.Type);
            Assert.Equal(3, routed.GameItemId);
            Assert.Empty(socket.SentFrames);
        }

        [Fact]
        public async Task SingleFrameOverflow_SendsMessageTooLargeAndClosesWithMessageTooBig()
        {
            var router = Substitute.For<IGameRouter>();
            var sender = CreateSender();
            var loop = new WebSocketMessageLoop(router, () => false, new CommandMessageFactory(), sender, 16);

            using var socket = new FakeWebSocket();
            socket.EnqueueText(new string('a', 17), endOfMessage: true);

            await loop.RunAsync(socket, () => { }, CancellationToken.None);

            router.DidNotReceiveWithAnyArgs().Route(default!);
            Assert.Equal(WebSocketCloseStatus.MessageTooBig, socket.CloseOutputStatus);
            Assert.Single(socket.SentFrames);
            using var doc = JsonDocument.Parse(socket.SentFrames[0]);
            Assert.Equal("message_too_large", doc.RootElement.GetProperty("code").GetString());
        }

        [Fact]
        public async Task FragmentedFrameOverflow_SendsMessageTooLargeAndClosesWithMessageTooBig()
        {
            var router = Substitute.For<IGameRouter>();
            var sender = CreateSender();
            var loop = new WebSocketMessageLoop(router, () => false, new CommandMessageFactory(), sender, 16);

            using var socket = new FakeWebSocket();
            socket.EnqueueText("1234567890", endOfMessage: false);
            socket.EnqueueText("1234567", endOfMessage: true);

            await loop.RunAsync(socket, () => { }, CancellationToken.None);

            router.DidNotReceiveWithAnyArgs().Route(default!);
            Assert.Equal(WebSocketCloseStatus.MessageTooBig, socket.CloseOutputStatus);
            Assert.Single(socket.SentFrames);
            using var doc = JsonDocument.Parse(socket.SentFrames[0]);
            Assert.Equal("message_too_large", doc.RootElement.GetProperty("code").GetString());
        }

        [Fact]
        public async Task IngressPaused_SendsBusyIngressPausedWithoutCallingRouter()
        {
            var router = Substitute.For<IGameRouter>();
            var sender = CreateSender();
            var loop = new WebSocketMessageLoop(router, () => true, new CommandMessageFactory(), sender, 1024);

            using var socket = new FakeWebSocket();
            socket.EnqueueText("""{"game_id":"g9","type":"Commands.Move"}""");
            socket.EnqueueClose();

            await loop.RunAsync(socket, () => { }, CancellationToken.None);

            router.DidNotReceiveWithAnyArgs().Route(default!);
            Assert.Single(socket.SentFrames);
            using var doc = JsonDocument.Parse(socket.SentFrames[0]);
            Assert.Equal("error", doc.RootElement.GetProperty("kind").GetString());
            Assert.Equal("busy", doc.RootElement.GetProperty("code").GetString());
            Assert.Equal("g9", doc.RootElement.GetProperty("game_id").GetString());
            Assert.Equal("ingress_paused", doc.RootElement.GetProperty("message").GetString());
        }

        [Fact]
        public async Task BinaryFrame_SendsUnsupportedFrameAndClosesWithInvalidMessageType()
        {
            var router = Substitute.For<IGameRouter>();
            var sender = CreateSender();
            var loop = new WebSocketMessageLoop(router, () => false, new CommandMessageFactory(), sender, 1024);

            using var socket = new FakeWebSocket();
            socket.EnqueueFrame([1, 2, 3], WebSocketMessageType.Binary, true);

            await loop.RunAsync(socket, () => { }, CancellationToken.None);

            Assert.Equal(WebSocketCloseStatus.InvalidMessageType, socket.CloseOutputStatus);
            Assert.Single(socket.SentFrames);
            using var doc = JsonDocument.Parse(socket.SentFrames[0]);
            Assert.Equal("unsupported_frame", doc.RootElement.GetProperty("code").GetString());
        }

        [Fact]
        public async Task DecoderAndRouterErrors_SendExpectedErrorFrames()
        {
            var router = Substitute.For<IGameRouter>();
            router.Route(Arg.Is<CommandDescription>(m => m.GameId == "missing")).Returns(MessageRoutingStatus.GameNotFound);
            router.Route(Arg.Is<CommandDescription>(m => m.GameId == "full")).Returns(MessageRoutingStatus.QueueFull);

            var sender = CreateSender();
            var loop = new WebSocketMessageLoop(router, () => false, new CommandMessageFactory(), sender, 1024);

            using var socket = new FakeWebSocket();
            socket.EnqueueText("{broken");
            socket.EnqueueText("""{"game_id":"g1"}""");
            socket.EnqueueText("""{"game_id":"missing","type":"Commands.Move"}""");
            socket.EnqueueText("""{"game_id":"full","type":"Commands.Move"}""");
            socket.EnqueueClose();

            await loop.RunAsync(socket, () => { }, CancellationToken.None);

            Assert.Equal(4, socket.SentFrames.Count);
            Assert.Equal("invalid_json", JsonDocument.Parse(socket.SentFrames[0]).RootElement.GetProperty("code").GetString());
            Assert.Equal("invalid_message", JsonDocument.Parse(socket.SentFrames[1]).RootElement.GetProperty("code").GetString());
            Assert.Equal("game_not_found", JsonDocument.Parse(socket.SentFrames[2]).RootElement.GetProperty("code").GetString());
            Assert.Equal("busy", JsonDocument.Parse(socket.SentFrames[3]).RootElement.GetProperty("code").GetString());
        }

        [Fact]
        public async Task DrainOutbound_WhenSocketNotOpen_ExitsWithoutSending()
        {
            var router = Substitute.For<IGameRouter>();
            var sender = CreateSender();
            sender.TrySend(Encoding.UTF8.GetBytes("""{"kind":"state"}"""));

            var loop = new WebSocketMessageLoop(router, () => false, new CommandMessageFactory(), sender, 1024);
            using var socket = new FakeWebSocket();
            socket.SetState(WebSocketState.Closed);

            var disconnected = false;
            await loop.RunAsync(socket, () => disconnected = true, CancellationToken.None);

            Assert.True(disconnected);
            Assert.Empty(socket.SentFrames);
        }

        [Fact]
        public async Task DrainOutbound_CatchesWebSocketExceptionAndOperationCanceledExceptionCleanly()
        {
            var router = Substitute.For<IGameRouter>();

            var send1Called = false;
            var disconnected1 = false;
            var sender1 = CreateSender();
            sender1.TrySend(Encoding.UTF8.GetBytes("one"));
            var loop1 = new WebSocketMessageLoop(router, () => false, new CommandMessageFactory(), sender1, 1024);
            using var socket1 = new FakeWebSocket
            {
                OnSendAsync = (_, _) =>
                {
                    send1Called = true;
                    return ValueTask.FromException(new WebSocketException("send-failed"));
                },
            };
            socket1.EnqueueClose();
            await loop1.RunAsync(socket1, () => disconnected1 = true, CancellationToken.None);

            Assert.True(send1Called);
            Assert.True(disconnected1);
            Assert.Equal(WebSocketCloseStatus.NormalClosure, socket1.CloseOutputStatus);

            var send2Called = false;
            var disconnected2 = false;
            var sender2 = CreateSender();
            sender2.TrySend(Encoding.UTF8.GetBytes("two"));
            var loop2 = new WebSocketMessageLoop(router, () => false, new CommandMessageFactory(), sender2, 1024);
            using var socket2 = new FakeWebSocket
            {
                OnSendAsync = (_, _) =>
                {
                    send2Called = true;
                    return ValueTask.FromException(new OperationCanceledException());
                },
            };
            socket2.EnqueueClose();
            await loop2.RunAsync(socket2, () => disconnected2 = true, CancellationToken.None);

            Assert.True(send2Called);
            Assert.True(disconnected2);
            Assert.Equal(WebSocketCloseStatus.NormalClosure, socket2.CloseOutputStatus);
        }

        [Fact]
        public async Task ReceiveLoop_HandlesWebSocketExceptionOperationCanceledAndPreCancelledToken()
        {
            var router = Substitute.For<IGameRouter>();

            var sender1 = CreateSender();
            var loop1 = new WebSocketMessageLoop(router, () => false, new CommandMessageFactory(), sender1, 1024);
            using var socket1 = new FakeWebSocket();
            socket1.EnqueueReceiveStep((_, _) => ValueTask.FromException<ValueWebSocketReceiveResult>(new WebSocketException("recv-failed")));
            var disconnected1 = false;
            await loop1.RunAsync(socket1, () => disconnected1 = true, CancellationToken.None);
            Assert.True(disconnected1);

            var sender2 = CreateSender();
            var loop2 = new WebSocketMessageLoop(router, () => false, new CommandMessageFactory(), sender2, 1024);
            using var socket2 = new FakeWebSocket();
            socket2.EnqueueReceiveStep((_, _) => ValueTask.FromException<ValueWebSocketReceiveResult>(new OperationCanceledException()));
            var disconnected2 = false;
            await loop2.RunAsync(socket2, () => disconnected2 = true, CancellationToken.None);
            Assert.True(disconnected2);

            var sender3 = CreateSender();
            var loop3 = new WebSocketMessageLoop(router, () => false, new CommandMessageFactory(), sender3, 1024);
            using var socket3 = new FakeWebSocket();
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var disconnected3 = false;
            await loop3.RunAsync(socket3, () => disconnected3 = true, cts.Token);
            Assert.True(disconnected3);
        }

        [Fact]
        public void ChannelClientMessageSending_SupportsDropOldestCompleteAndNullCheck()
        {
            Assert.Throws<ArgumentNullException>(() => new ChannelClientMessageSending(null!));

            var sender = CreateSender(capacity: 2);
            Assert.True(sender.TrySend(new byte[] { 1 }));
            Assert.True(sender.TrySend(new byte[] { 2 }));
            Assert.True(sender.TrySend(new byte[] { 3 }));

            Assert.True(sender.Reader.TryRead(out var first));
            Assert.Equal(new byte[] { 2 }, first.ToArray());
            Assert.True(sender.Reader.TryRead(out var second));
            Assert.Equal(new byte[] { 3 }, second.ToArray());

            sender.Complete();
            Assert.False(sender.TrySend(new byte[] { 4 }));
        }

        [Fact]
        public void ServerExceptionRecording_RecordsAndSnapshotsThreadSafely()
        {
            var recording = new ServerExceptionRecording();
            var command = Substitute.For<ICommand>();

            Assert.Throws<ArgumentNullException>(() => recording.Record(null!, new InvalidOperationException()));
            Assert.Throws<ArgumentNullException>(() => recording.Record(command, null!));

            var ex1 = new InvalidOperationException("first");
            var ex2 = new ArgumentException("second");
            recording.Record(command, ex1);
            recording.Record(command, ex2);

            var snapshot = recording.Snapshot();
            Assert.Equal(2, snapshot.Count);
            Assert.Contains(ex1, snapshot);
            Assert.Contains(ex2, snapshot);
        }

        [Fact]
        public async Task RunAsync_WhenRouterThrowsUnexpectedException_CompletesSenderCallsOnDisconnectedAndPropagates()
        {
            var router = Substitute.For<IGameRouter>();
            router.Route(Arg.Any<CommandDescription>()).Returns(_ => throw new InvalidOperationException("router-boom"));

            var sender = CreateSender();
            var loop = new WebSocketMessageLoop(router, () => false, new CommandMessageFactory(), sender, 1024);

            using var socket = new FakeWebSocket();
            socket.EnqueueText("""{"game_id":"g1","type":"Commands.Move","game_item_id":1,"step":0}""");

            var disconnected = false;
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => loop.RunAsync(socket, () => disconnected = true, CancellationToken.None));

            Assert.Equal("router-boom", ex.Message);
            Assert.True(disconnected);
            Assert.True(sender.Reader.Completion.IsCompleted);
        }

        [Fact]
        public async Task DrainOutbound_WhenSocketIsCloseReceived_StillDeliversQueuedFrame()
        {
            var router = Substitute.For<IGameRouter>();
            var sender = CreateSender();
            var payload = """{"kind":"state","game_id":"g1"}""";
            sender.TrySend(Encoding.UTF8.GetBytes(payload));

            var loop = new WebSocketMessageLoop(router, () => false, new CommandMessageFactory(), sender, 1024);
            using var socket = new FakeWebSocket();
            socket.SetState(WebSocketState.CloseReceived);

            var disconnected = false;
            await loop.RunAsync(socket, () => disconnected = true, CancellationToken.None);

            Assert.True(disconnected);
            Assert.Single(socket.SentFrames);
            Assert.Equal(payload, socket.SentFrames[0]);
        }

        [Fact]
        public async Task FragmentedTextFrame_InterruptedByClose_ClosesNormallyWithoutDecodingTruncatedPayload()
        {
            var router = Substitute.For<IGameRouter>();
            var sender = CreateSender();
            var loop = new WebSocketMessageLoop(router, () => false, new CommandMessageFactory(), sender, 1024);

            using var socket = new FakeWebSocket();
            socket.EnqueueText("{\"game_id\":\"g1\",", endOfMessage: false);
            socket.EnqueueClose();

            var disconnected = false;
            await loop.RunAsync(socket, () => disconnected = true, CancellationToken.None);

            Assert.True(disconnected);
            Assert.Equal(WebSocketCloseStatus.NormalClosure, socket.CloseOutputStatus);
            Assert.Empty(socket.SentFrames);
            router.DidNotReceiveWithAnyArgs().Route(default!);
        }
    }
}
