using System.Net.WebSockets;
using SpaceBattle.Lib;

namespace SpaceBattle.Server
{
    public sealed class WebSocketMessageLoop
    {
        private readonly IGameRouter _router;
        private readonly Func<bool> _isIngressPaused;
        private readonly ICommandMessageDecoding _decoder;
        private readonly ChannelClientMessageSending _sender;
        private readonly int _maxMessageBytes;

        public WebSocketMessageLoop(
            IGameRouter router,
            Func<bool> isIngressPaused,
            ICommandMessageDecoding decoder,
            ChannelClientMessageSending sender,
            int maxMessageBytes)
        {
            ArgumentNullException.ThrowIfNull(router);
            ArgumentNullException.ThrowIfNull(isIngressPaused);
            ArgumentNullException.ThrowIfNull(decoder);
            ArgumentNullException.ThrowIfNull(sender);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxMessageBytes);

            _router = router;
            _isIngressPaused = isIngressPaused;
            _decoder = decoder;
            _sender = sender;
            _maxMessageBytes = maxMessageBytes;
        }

        public async Task RunAsync(WebSocket socket, Action onDisconnected, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(socket);
            ArgumentNullException.ThrowIfNull(onDisconnected);

            var loopCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var loopToken = loopCts.Token;
            var drainTask = DrainOutboundAsync(socket, loopToken);
            var buffer = new byte[_maxMessageBytes + 1];

            try
            {
                while (!loopToken.IsCancellationRequested && socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(buffer.AsMemory(), loopToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _sender.Complete();
                        await drainTask;
                        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, loopToken);
                        break;
                    }

                    if (result.MessageType != WebSocketMessageType.Text)
                    {
                        await CloseWithErrorAsync(
                            socket,
                            drainTask,
                            "unsupported_frame",
                            WebSocketCloseStatus.InvalidMessageType,
                            loopToken);
                        break;
                    }

                    if (result.EndOfMessage)
                    {
                        if (result.Count > _maxMessageBytes)
                        {
                            await CloseWithErrorAsync(
                                socket,
                                drainTask,
                                "message_too_large",
                                WebSocketCloseStatus.MessageTooBig,
                                loopToken);
                            break;
                        }

                        HandlePayload(buffer.AsSpan(0, result.Count));
                        continue;
                    }

                    var stream = new MemoryStream();
                    try
                    {
                        var totalBytes = 0;
                        var aborted = false;
                        var current = result;

                        while (true)
                        {
                            totalBytes += current.Count;
                            if (totalBytes > _maxMessageBytes)
                            {
                                await CloseWithErrorAsync(
                                    socket,
                                    drainTask,
                                    "message_too_large",
                                    WebSocketCloseStatus.MessageTooBig,
                                    loopToken);
                                aborted = true;
                                break;
                            }

                            stream.Write(buffer, 0, current.Count);

                            if (current.EndOfMessage)
                            {
                                break;
                            }

                            current = await socket.ReceiveAsync(buffer.AsMemory(), loopToken);
                            if (current.MessageType == WebSocketMessageType.Close)
                            {
                                _sender.Complete();
                                await drainTask;
                                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, loopToken);
                                aborted = true;
                                break;
                            }
                        }

                        if (aborted)
                        {
                            break;
                        }

                        HandlePayload(stream.GetBuffer().AsSpan(0, (int)stream.Length));
                    }
                    finally
                    {
                        stream.Dispose();
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (WebSocketException)
            {
            }
            finally
            {
                _sender.Complete();
                await drainTask;
                loopCts.Cancel();
                loopCts.Dispose();
                onDisconnected();
            }
        }

        private async Task CloseWithErrorAsync(
            WebSocket socket,
            Task drainTask,
            string errorCode,
            WebSocketCloseStatus closeStatus,
            CancellationToken ct)
        {
            _sender.TrySend(_decoder.EncodeError(errorCode, string.Empty, errorCode));
            _sender.Complete();
            await drainTask;
            await socket.CloseOutputAsync(closeStatus, errorCode, ct);
        }

        private void HandlePayload(ReadOnlySpan<byte> payload)
        {
            if (!_decoder.TryDecode(payload, out var message, out var errorCode))
            {
                _sender.TrySend(_decoder.EncodeError(errorCode, string.Empty, errorCode));
                return;
            }

            if (_isIngressPaused())
            {
                _sender.TrySend(_decoder.EncodeError("busy", message.GameId, "ingress_paused"));
                return;
            }

            var status = _router.Route(message);
            if (status == MessageRoutingStatus.GameNotFound)
            {
                _sender.TrySend(_decoder.EncodeError("game_not_found", message.GameId, "game_not_found"));
            }
            else if (status == MessageRoutingStatus.QueueFull)
            {
                _sender.TrySend(_decoder.EncodeError("busy", message.GameId, "busy"));
            }
        }

        private async Task DrainOutboundAsync(WebSocket socket, CancellationToken ct)
        {
            try
            {
                await foreach (var payload in _sender.Reader.ReadAllAsync(ct))
                {
                    if (socket.State != WebSocketState.Open && socket.State != WebSocketState.CloseReceived)
                    {
                        break;
                    }

                    await socket.SendAsync(payload, WebSocketMessageType.Text, true, ct);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (WebSocketException)
            {
            }
        }
    }
}
