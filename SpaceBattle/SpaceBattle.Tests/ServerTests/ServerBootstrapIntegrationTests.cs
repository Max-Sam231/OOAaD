using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using App;
using App.Scopes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SpaceBattle.Lib;
using SpaceBattle.Server;

namespace SpaceBattle.Test
{
    public class ServerBootstrapIntegrationTests : IDisposable
    {
        private readonly List<string> _tempDirs = [];

        public void Dispose()
        {
            new InitCommand().Execute();
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Clear").Execute();

            foreach (var dir in _tempDirs)
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
        }

        private static int GetFreeTcpPort()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        private string CreateTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "SpaceBattle.ServerTests." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            _tempDirs.Add(dir);
            return dir;
        }

        private static void EmitPluginDll(string assemblyName, string markerValue, string outputPath)
        {
            var source = $$"""
                using System;
                using App;

                namespace DynamicServerPlugin
                {
                    public sealed class RegisterPluginMarkerCommand : ICommand
                    {
                        public void Execute()
                        {
                            Ioc.Resolve<ICommand>(
                                "IoC.Register",
                                "Plugin.Marker",
                                (Func<object[], object>)(_ => "{{markerValue}}")).Execute();
                        }
                    }
                }
                """;

            var references = ScopedAssemblyCatalogFactory.CreateDefault().MetadataReferences;
            var compilation = CSharpCompilation.Create(
                assemblyName,
                [CSharpSyntaxTree.ParseText(source)],
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var result = compilation.Emit(outputPath);
            if (!result.Success)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));
            }
        }

        private static async Task WaitForHealthAsync(HttpClient client, Uri baseUri, Task serverTask, CancellationToken ct)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                if (serverTask.IsFaulted)
                {
                    await serverTask;
                }

                try
                {
                    using var response = await client.GetAsync(new Uri(baseUri, "/health"), ct);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return;
                    }
                }
                catch (HttpRequestException)
                {
                }

                await Task.Delay(25, ct);
            }

            throw new TimeoutException("Server did not become healthy in time.");
        }

        private static async Task<string> ReceiveTextAsync(ClientWebSocket ws, CancellationToken ct)
        {
            var buffer = new byte[8192];
            var result = await ws.ReceiveAsync(buffer.AsMemory(), ct);
            return Encoding.UTF8.GetString(buffer, 0, result.Count);
        }

        [Fact]
        public async Task ProgramMain_ServesHealthAndStopsGracefullyOnAdminShutdown()
        {
            var port = GetFreeTcpPort();
            var baseUri = new Uri($"http://127.0.0.1:{port}");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var client = new HttpClient();

            var mainTask = Program.Main(["--urls", baseUri.ToString().TrimEnd('/')]);

            await WaitForHealthAsync(client, baseUri, mainTask, cts.Token);

            using var healthResponse = await client.GetAsync(new Uri(baseUri, "/health"), cts.Token);
            Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
            var healthBody = await healthResponse.Content.ReadAsStringAsync(cts.Token);
            using (var doc = JsonDocument.Parse(healthBody))
            {
                Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
            }

            using var shutdownResponse = await client.PostAsync(new Uri(baseUri, "/admin/shutdown"), null, cts.Token);
            Assert.Equal(HttpStatusCode.OK, shutdownResponse.StatusCode);
            var shutdownBody = await shutdownResponse.Content.ReadAsStringAsync(cts.Token);
            using (var doc = JsonDocument.Parse(shutdownBody))
            {
                Assert.Equal("shutting_down", doc.RootElement.GetProperty("status").GetString());
            }

            await mainTask.WaitAsync(cts.Token);
        }

        [Fact]
        public async Task ServerBootstrap_WebSocket_HandlesNonWs400_SubscribedTickPush_AndUnsubscribedClient()
        {
            var startedTcs = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            ServerThread? worker = null;
            ICommand? gameBaseInitializer = null;
            var emptyCommandResolvedInGame = false;

            var serverTask = ServerBootstrap.RunAsync(
                ["--urls", "http://127.0.0.1:0"],
                cts.Token,
                _ =>
                {
                    gameBaseInitializer = Ioc.Resolve<ICommand>("Endpoint.GameBase.Initializer");
                    worker = Ioc.Resolve<ServerThread>("Server.Thread.Create");
                    var registry = new GameReceiverRegistry();
                    var gameScope = Ioc.Resolve<object>("Scopes.Game.Create");
                    var quantumState = new GameQuantumState(16);
                    var repo = new EnumerableGameObjectRepository();
                    repo.Set(1, new Dictionary<string, object> { ["Position"] = new Vector(1, 2) });

                    new InSeparateScopeCommand(
                        gameScope,
                        new DelegatingCommand(() =>
                        {
                            new RegisterIoCDependencyGameQuantum(quantumState).Execute();
                            new RegisterIoCDependencyGameStep().Execute();
                            Ioc.Resolve<ICommand>("Commands.Empty").Execute();
                            emptyCommandResolvedInGame = true;
                            Ioc.Resolve<ICommand>(
                                "IoC.Register",
                                "Game.Repository.ReadOnly",
                                (Func<object[], object>)(_ => repo)).Execute();
                            Ioc.Resolve<ICommand>(
                                "IoC.Register",
                                "Commands.Move",
                                (Func<object[], object>)(args =>
                                {
                                    var item = (IDictionary<string, object>)args[0];
                                    return new DelegatingCommand(() => item["Position"] = new Vector(4, 6));
                                })).Execute();
                        })).Execute();

                    var game = new Game();
                    var turn = new GameQuantumCommand(
                        new InSeparateScopeCommand(gameScope, game),
                        quantumState,
                        worker.Scheduler);
                    var schedulingReceiver = new SchedulingCommandReceiver(game, worker.Scheduler, turn);
                    var target = Ioc.Resolve<IGameCommandSending>("Endpoint.Target.Local.Create", schedulingReceiver);
                    registry.Set("g1", target);

                    var tryEnqueue = Ioc.Resolve<Func<ICommand, bool>>("Server.Thread.TryEnqueue", worker);
                    var route = Ioc.Resolve<IGameEndpointRoute>("Endpoint.Route.Create", tryEnqueue, registry);
                    Ioc.Resolve<IGameRouter>("Server.Router").Register("g1", route);

                    worker.Start();
                },
                uri => startedTcs.TrySetResult(uri));

            var baseUri = await startedTcs.Task.WaitAsync(cts.Token);
            Assert.NotNull(gameBaseInitializer);
            Assert.True(emptyCommandResolvedInGame);

            using var http = new HttpClient();
            using var nonWs = await http.GetAsync(new Uri(baseUri, "/ws"), cts.Token);
            Assert.Equal(HttpStatusCode.BadRequest, nonWs.StatusCode);

            using (var ws = new ClientWebSocket())
            {
                await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{baseUri.Port}/ws?gameId=g1"), cts.Token);
                var payload = Encoding.UTF8.GetBytes("""{"game_id":"g1","type":"Commands.Move","game_item_id":1,"step":0}""");
                await ws.SendAsync(payload, WebSocketMessageType.Text, true, cts.Token);

                var stateJson = await ReceiveTextAsync(ws, cts.Token);
                using var doc = JsonDocument.Parse(stateJson);
                Assert.Equal("state", doc.RootElement.GetProperty("kind").GetString());
                Assert.Equal("g1", doc.RootElement.GetProperty("game_id").GetString());
                Assert.Equal(1L, doc.RootElement.GetProperty("step").GetInt64());
                Assert.Equal("running", doc.RootElement.GetProperty("status").GetString());

                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cts.Token);
            }

            using (var wsNoGame = new ClientWebSocket())
            {
                await wsNoGame.ConnectAsync(new Uri($"ws://127.0.0.1:{baseUri.Port}/ws"), cts.Token);
                var payload = Encoding.UTF8.GetBytes("""{"game_id":"unknown","type":"Commands.Move","game_item_id":1,"step":0}""");
                await wsNoGame.SendAsync(payload, WebSocketMessageType.Text, true, cts.Token);

                var errorJson = await ReceiveTextAsync(wsNoGame, cts.Token);
                using var doc = JsonDocument.Parse(errorJson);
                Assert.Equal("game_not_found", doc.RootElement.GetProperty("code").GetString());

                await wsNoGame.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cts.Token);
            }

            using var shutdownResponse = await http.PostAsync(new Uri(baseUri, "/admin/shutdown"), null, cts.Token);
            Assert.Equal(HttpStatusCode.OK, shutdownResponse.StatusCode);
            await serverTask.WaitAsync(cts.Token);

            worker!.Receive(new SoftStopServerThreadCommand(worker.State));
            Assert.True(worker.Join(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task ServerBootstrap_StartupPluginsDir_HotReloadAndShutdownSnapshotPath_WorkEndToEnd()
        {
            var dir1 = CreateTempDir();
            var dir2 = CreateTempDir();
            EmitPluginDll("ServerPluginV1", "loaded-v1", Path.Combine(dir1, "ServerPluginV1.dll"));
            EmitPluginDll("ServerPluginV2", "loaded-v2", Path.Combine(dir2, "ServerPluginV2.dll"));

            var startedTcs = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            ScopeParentHolder? holder = null;
            string? capturedSnapshotPath = null;

            var serverTask = ServerBootstrap.RunAsync(
                ["--urls", "http://127.0.0.1:0", "--plugins-dir", dir1],
                cts.Token,
                _ =>
                {
                    holder = Ioc.Resolve<ScopeParentHolder>("Server.Scope.Holder");
                    Ioc.Resolve<ICommand>(
                        "IoC.Register",
                        "Server.Admin.Shutdown",
                        (Func<object[], object>)(args => new DelegatingCommand(() => capturedSnapshotPath = (string?)args[0])))
                        .Execute();
                },
                uri => startedTcs.TrySetResult(uri));

            var baseUri = await startedTcs.Task.WaitAsync(cts.Token);

            string markerV1 = string.Empty;
            new InSeparateScopeCommand(
                holder!.Current,
                new DelegatingCommand(() => markerV1 = Ioc.Resolve<string>("Plugin.Marker"))).Execute();
            Assert.Equal("loaded-v1", markerV1);

            using var http = new HttpClient();
            using var badReload = await http.PostAsync(new Uri(baseUri, "/admin/hot-reload"), null, cts.Token);
            Assert.Equal(HttpStatusCode.BadRequest, badReload.StatusCode);

            using var okReload = await http.PostAsync(
                new Uri(baseUri, $"/admin/hot-reload?pluginsDir={Uri.EscapeDataString(dir2)}"),
                null,
                cts.Token);
            Assert.Equal(HttpStatusCode.OK, okReload.StatusCode);
            var reloadBody = await okReload.Content.ReadAsStringAsync(cts.Token);
            using (var doc = JsonDocument.Parse(reloadBody))
            {
                Assert.Equal("reloaded", doc.RootElement.GetProperty("status").GetString());
            }

            string markerV2 = string.Empty;
            new InSeparateScopeCommand(
                holder.Current,
                new DelegatingCommand(() => markerV2 = Ioc.Resolve<string>("Plugin.Marker"))).Execute();
            Assert.Equal("loaded-v2", markerV2);

            using var shutdownResponse = await http.PostAsync(
                new Uri(baseUri, "/admin/shutdown?snapshotPath=snap.json"),
                null,
                cts.Token);
            Assert.Equal(HttpStatusCode.OK, shutdownResponse.StatusCode);

            await serverTask.WaitAsync(cts.Token);
            Assert.Equal("snap.json", capturedSnapshotPath);
        }

        [Fact]
        public async Task ServerBootstrap_TwoConcurrentInstancesInSameProcess_DoNotCollide()
        {
            var started1 = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
            var started2 = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            var server1 = ServerBootstrap.RunAsync(
                ["--urls", "http://127.0.0.1:0"],
                cts.Token,
                null,
                uri => started1.TrySetResult(uri));
            var server2 = ServerBootstrap.RunAsync(
                ["--urls", "http://127.0.0.1:0"],
                cts.Token,
                null,
                uri => started2.TrySetResult(uri));

            var uri1 = await started1.Task.WaitAsync(cts.Token);
            var uri2 = await started2.Task.WaitAsync(cts.Token);

            using var http = new HttpClient();
            Assert.Equal(HttpStatusCode.OK, (await http.GetAsync(new Uri(uri1, "/health"), cts.Token)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await http.GetAsync(new Uri(uri2, "/health"), cts.Token)).StatusCode);

            Assert.Equal(HttpStatusCode.OK, (await http.PostAsync(new Uri(uri1, "/admin/shutdown"), null, cts.Token)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await http.PostAsync(new Uri(uri2, "/admin/shutdown"), null, cts.Token)).StatusCode);

            await Task.WhenAll(server1, server2).WaitAsync(cts.Token);
        }

        [Fact]
        public async Task ServerBootstrap_RecordsUnhandledExceptionsFromServerThreadAndGame()
        {
            var startedTcs = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            ServerThread? worker = null;
            IServerExceptionRecording? exceptions = null;
            IGameRouter? router = null;

            var serverTask = ServerBootstrap.RunAsync(
                ["--urls", "http://127.0.0.1:0"],
                cts.Token,
                _ =>
                {
                    exceptions = Ioc.Resolve<IServerExceptionRecording>("Server.Exceptions");
                    router = Ioc.Resolve<IGameRouter>("Server.Router");
                    worker = Ioc.Resolve<ServerThread>("Server.Thread.Create");

                    var registry = new GameReceiverRegistry();
                    var gameScope = Ioc.Resolve<object>("Scopes.Game.Create");
                    var quantumState = new GameQuantumState(16);
                    var repo = new EnumerableGameObjectRepository();
                    repo.Set(1, new Dictionary<string, object>());

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
                                "Commands.Explode",
                                (Func<object[], object>)(_ => new DelegatingCommand(() => throw new InvalidOperationException("game-boom"))))
                                .Execute();
                        })).Execute();

                    var game = new Game();
                    var turn = new GameQuantumCommand(
                        new InSeparateScopeCommand(gameScope, game),
                        quantumState,
                        worker.Scheduler);
                    var schedulingReceiver = new SchedulingCommandReceiver(game, worker.Scheduler, turn);
                    var target = Ioc.Resolve<IGameCommandSending>("Endpoint.Target.Local.Create", schedulingReceiver);
                    registry.Set("g-err", target);

                    var tryEnqueue = Ioc.Resolve<Func<ICommand, bool>>("Server.Thread.TryEnqueue", worker);
                    var route = Ioc.Resolve<IGameEndpointRoute>("Endpoint.Route.Create", tryEnqueue, registry);
                    router.Register("g-err", route);

                    worker.Start();
                },
                uri => startedTcs.TrySetResult(uri));

            var baseUri = await startedTcs.Task.WaitAsync(cts.Token);

            worker!.Receive(new DelegatingCommand(() => throw new InvalidOperationException("thread-boom")));
            Assert.Equal(
                MessageRoutingStatus.Accepted,
                router!.Route(new CommandDescription
                {
                    GameId = "g-err",
                    Type = "Commands.Explode",
                    GameItemId = 1,
                    Step = 0,
                }));

            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (exceptions!.Snapshot().Count < 2 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10, cts.Token);
            }

            var recorded = exceptions.Snapshot();
            Assert.Equal(2, recorded.Count);
            Assert.Contains(recorded, ex => ex.Message == "thread-boom");
            Assert.Contains(recorded, ex => ex.Message == "game-boom");

            using var http = new HttpClient();
            await http.PostAsync(new Uri(baseUri, "/admin/shutdown"), null, cts.Token);
            await serverTask.WaitAsync(cts.Token);

            worker.Receive(new SoftStopServerThreadCommand(worker.State));
            Assert.True(worker.Join(TimeSpan.FromSeconds(5)));
        }
    }
}
