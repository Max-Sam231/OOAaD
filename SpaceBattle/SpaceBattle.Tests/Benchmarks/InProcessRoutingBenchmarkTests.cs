using System.Diagnostics;
using App;
using App.Scopes;
using SpaceBattle.Lib;
using SpaceBattle.Server;
using Xunit.Abstractions;

namespace SpaceBattle.Test
{
    [Collection("Benchmarks")]
    public class InProcessRoutingBenchmarkTests : IDisposable
    {
        private readonly ITestOutputHelper _output;

        public InProcessRoutingBenchmarkTests(ITestOutputHelper output)
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
        public void InProcessRouting_Processes100kMessagesAboveThroughputFloor()
        {
            const int gameCount = 4;
            const int messagesPerGame = 25_000;
            const int totalMessages = gameCount * messagesPerGame;

            var rootScope = Ioc.Resolve<object>("IoC.Scope.Current");
            var serverScope = Ioc.Resolve<object>("IoC.Scope.Create", rootScope);
            var connections = new GameConnectionRegistry();
            var exceptions = new ServerExceptionRecording();

            var baseInitializer = new DelegatingCommand(() =>
            {
                new RegisterIoCDependencySnapshotCodecs().Execute();
                new RegisterIoCDependencyGameRules().Execute();
                new RegisterIoCDependencyEndpointGameBase(connections, exceptions.Record).Execute();
            });

            var threads = new ServerThread?[gameCount];
            using var allDone = new CountdownEvent(gameCount);
            var processed = 0;
            IGameRouter router = null!;
            var sw = new Stopwatch();

            try
            {
                new InSeparateScopeCommand(
                    serverScope,
                    new DelegatingCommand(() =>
                    {
                        new RegisterIoCDependencyServerThread().Execute();
                        new RegisterIoCDependencyEndpoint().Execute();
                        Ioc.Resolve<ICommand>(
                            "IoC.Register",
                            "Exception.Handle",
                            (Func<object[], object>)(args => new DelegatingCommand(
                                () => exceptions.Record((ICommand)args[0], (Exception)args[1])))).Execute();
                        new RegisterIoCDependencyScopeHierarchy(rootScope, baseInitializer).Execute();

                        router = Ioc.Resolve<IGameRouter>("Server.Router");

                        for (var i = 0; i < gameCount; i++)
                        {
                            var gameId = $"bench-game-{i}";
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
                                if (localCount == messagesPerGame)
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
                    })).Execute();

                var batches = new CommandDescription[gameCount][];
                for (var g = 0; g < gameCount; g++)
                {
                    var gameId = $"bench-game-{g}";
                    var batch = new CommandDescription[messagesPerGame];
                    for (var s = 0; s < messagesPerGame; s++)
                    {
                        batch[s] = new CommandDescription
                        {
                            GameId = gameId,
                            Type = "Commands.Tick",
                            GameItemId = 1,
                            Step = s,
                        };
                    }

                    batches[g] = batch;
                }

                sw.Start();
                Parallel.For(0, gameCount, g =>
                {
                    var batch = batches[g];
                    for (var s = 0; s < batch.Length; s++)
                    {
                        var status = router.Route(batch[s]);
                        if (status != MessageRoutingStatus.Accepted)
                        {
                            throw new InvalidOperationException($"Unexpected status: {status}");
                        }
                    }
                });

                Assert.True(allDone.Wait(TimeSpan.FromSeconds(30)));
                sw.Stop();
            }
            finally
            {
                new InSeparateScopeCommand(
                    serverScope,
                    new DelegatingCommand(() =>
                    {
                        foreach (var worker in threads)
                        {
                            if (worker != null)
                            {
                                Ioc.Resolve<ICommand>("Server.Thread.Stop.Soft", worker).Execute();
                            }
                        }
                    })).Execute();

                foreach (var worker in threads)
                {
                    if (worker != null)
                    {
                        Assert.True(worker.Join(TimeSpan.FromSeconds(10)));
                    }
                }
            }

            var throughput = totalMessages / sw.Elapsed.TotalSeconds;
            _output.WriteLine($"In-process routing: {totalMessages} msgs in {sw.Elapsed.TotalMilliseconds:F1} ms ({throughput:F0} msg/s)");

            Assert.Empty(exceptions.Snapshot());
            Assert.Equal(totalMessages, processed);
            Assert.True(throughput >= 50_000, $"Expected >= 50,000 msg/s, actual {throughput:F0} msg/s");
        }
    }
}
