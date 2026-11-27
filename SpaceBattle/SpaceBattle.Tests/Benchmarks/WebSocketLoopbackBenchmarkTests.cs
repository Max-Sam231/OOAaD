using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using App;
using App.Scopes;
using SpaceBattle.Lib;
using SpaceBattle.Server;
using Xunit.Abstractions;

namespace SpaceBattle.Test
{
    [Collection("Benchmarks")]
    public class WebSocketLoopbackBenchmarkTests : IDisposable
    {
        private readonly ITestOutputHelper _output;

        public WebSocketLoopbackBenchmarkTests(ITestOutputHelper output)
        {
            _output = output;
            new InitCommand().Execute();
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Clear").Execute();
        }

        public void Dispose()
        {
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Clear").Execute();
        }

        [Fact]
        public async Task WebSocketLoopback_Processes40kFramesAboveThroughputFloor()
        {
            const int clientCount = 4;
            const int messagesPerClient = 10_000;
            const int totalMessages = clientCount * messagesPerClient;

            var startedTcs = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            using var allDone = new CountdownEvent(clientCount);

            var threads = new ServerThread?[clientCount];
            var clients = new ClientWebSocket?[clientCount];
            var processed = 0;
            IServerExceptionRecording? exceptions = null;

            var serverTask = ServerBootstrap.RunAsync(
                ["--urls", "http://127.0.0.1:0"],
                cts.Token,
                _ =>
                {
                    exceptions = Ioc.Resolve<IServerExceptionRecording>("Server.Exceptions");
                    var router = Ioc.Resolve<IGameRouter>("Server.Router");

                    for (var i = 0; i < clientCount; i++)
                    {
                        var gameId = $"ws-bench-{i}";
                        var worker = Ioc.Resolve<ServerThread>("Server.Thread.Create");
                        threads[i] = worker;

                        var registry = new GameReceiverRegistry();
                        var gameScope = Ioc.Resolve<object>("Scopes.Game.Create");
                        var quantumState = new GameQuantumState(1024);
                        var repo = new EnumerableGameObjectRepository();
                        repo.Set(1, new Dictionary<string, object>());
                        var localCount = 0;
                        var tickCommand = new DelegatingCommand(() =>
                        {
                            localCount++;
                            if (localCount == messagesPerClient)
                            {
                                Interlocked.Add(ref processed, localCount);
                                allDone.Signal();
                            }
                        });

                        new InSeparateScopeCommand(
                            gameScope,
                            new DelegatingCommand(() =>
                            {
                                new RegisterIoCDependencyGameQuantum(quantumState).Execute();
                                new RegisterIoCDependencyGameStep().Execute();
                                Ioc.Resolve<ICommand>(
                                    "IoC.Register",
                                    "Game.Repository.ReadOnly",
                                    (Func<object[], object>)(_ => repo)).Execute();
                                Ioc.Resolve<ICommand>(
                                    "IoC.Register",
                                    "Commands.Tick",
                                    (Func<object[], object>)(_ => tickCommand)).Execute();
                            })).Execute();

                        var game = new Game();
                        var turn = new GameQuantumCommand(
                            new InSeparateScopeCommand(gameScope, game),
                            quantumState,
                            worker.Scheduler);
                        var schedulingReceiver = new SchedulingCommandReceiver(game, worker.Scheduler, turn);
                        var target = Ioc.Resolve<IGameCommandSending>("Endpoint.Target.Local.Create", schedulingReceiver);
                        registry.Set(gameId, target);

                        var tryEnqueue = Ioc.Resolve<Func<ICommand, bool>>("Server.Thread.TryEnqueue", worker);
                        var route = Ioc.Resolve<IGameEndpointRoute>("Endpoint.Route.Create", tryEnqueue, registry);
                        router.Register(gameId, route);

                        worker.Start();
                    }
                },
                uri => startedTcs.TrySetResult(uri));

            Uri? baseUri = null;
            var sw = new Stopwatch();

            try
            {
                baseUri = await startedTcs.Task.WaitAsync(cts.Token);

                var serializedFrames = new byte[clientCount][][];
                for (var c = 0; c < clientCount; c++)
                {
                    var gameId = $"ws-bench-{c}";
                    var frames = new byte[messagesPerClient][];
                    for (var s = 0; s < messagesPerClient; s++)
                    {
                        frames[s] = Encoding.UTF8.GetBytes(
                            $$"""{"game_id":"{{gameId}}","type":"Commands.Tick","game_item_id":1,"step":{{s}}}""");
                    }

                    serializedFrames[c] = frames;
                }

                for (var c = 0; c < clientCount; c++)
                {
                    var ws = new ClientWebSocket();
                    await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{baseUri.Port}/ws"), cts.Token);
                    clients[c] = ws;
                }

                sw.Start();
                var sendTasks = new Task[clientCount];
                for (var c = 0; c < clientCount; c++)
                {
                    var ws = clients[c]!;
                    var frames = serializedFrames[c];
                    sendTasks[c] = Task.Run(
                        async () =>
                        {
                            for (var s = 0; s < frames.Length; s++)
                            {
                                await ws.SendAsync(frames[s], WebSocketMessageType.Text, true, cts.Token);
                            }
                        },
                        cts.Token);
                }

                await Task.WhenAll(sendTasks);
                Assert.True(allDone.Wait(TimeSpan.FromSeconds(30)));
                sw.Stop();
            }
            finally
            {
                for (var c = 0; c < clients.Length; c++)
                {
                    var ws = clients[c];
                    if (ws != null)
                    {
                        if (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived)
                        {
                            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cts.Token);
                        }

                        ws.Dispose();
                    }
                }

                if (baseUri != null)
                {
                    using var http = new HttpClient();
                    using var shutdownResponse = await http.PostAsync(new Uri(baseUri, "/admin/shutdown"), null, cts.Token);
                    Assert.Equal(HttpStatusCode.OK, shutdownResponse.StatusCode);
                    await serverTask.WaitAsync(cts.Token);
                }
                else
                {
                    cts.Cancel();
                }

                foreach (var worker in threads)
                {
                    if (worker != null)
                    {
                        worker.Receive(new SoftStopServerThreadCommand(worker.State));
                    }
                }

                foreach (var worker in threads)
                {
                    if (worker != null)
                    {
                        Assert.True(worker.Join(TimeSpan.FromSeconds(10)));
                    }
                }
            }

            var throughput = totalMessages / sw.Elapsed.TotalSeconds;
            _output.WriteLine($"WebSocket loopback: {totalMessages} frames in {sw.Elapsed.TotalMilliseconds:F1} ms ({throughput:F0} msg/s)");

            Assert.Empty(exceptions!.Snapshot());
            Assert.Equal(totalMessages, processed);
            Assert.True(throughput >= 20_000, $"Expected >= 20,000 msg/s, actual {throughput:F0} msg/s");
        }
    }
}
