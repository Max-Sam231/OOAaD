using App;
using App.Scopes;
using NSubstitute;
using SpaceBattle.Lib;

namespace SpaceBattle.Test
{
    public class GameRouterAndDeliveryTests : IDisposable
    {
        private readonly object _rootScope;

        public GameRouterAndDeliveryTests()
        {
            new InitCommand().Execute();
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Clear").Execute();
            _rootScope = Ioc.Resolve<object>("IoC.Scope.Current");
        }

        public void Dispose()
        {
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Clear").Execute();
        }

        [Fact]
        public void GameRouter_RegisterRouteTryGetUnregister_CoversAllOutcomesAndGuards()
        {
            var router = new GameRouter();
            var route = Substitute.For<IGameEndpointRoute>();
            var message = new CommandDescription { GameId = "game-1", Type = "Commands.Move" };

            Assert.Throws<ArgumentException>(() => router.Register("", route));
            Assert.Throws<ArgumentException>(() => router.Register("   ", route));
            Assert.Throws<ArgumentNullException>(() => router.Register("game-1", null!));
            Assert.Throws<ArgumentNullException>(() => router.Route(null!));
            Assert.Throws<ArgumentException>(() => router.TryGetRoute("", out _));
            Assert.Throws<ArgumentException>(() => router.Unregister(""));

            Assert.Equal(MessageRoutingStatus.GameNotFound, router.Route(message));
            Assert.False(router.TryGetRoute("game-1", out var missingRoute));
            Assert.Null(missingRoute);
            Assert.False(router.Unregister("game-1"));

            router.Register("game-1", route);
            Assert.True(router.TryGetRoute("game-1", out var foundRoute));
            Assert.Same(route, foundRoute);

            route.TryRoute(message).Returns(true);
            Assert.Equal(MessageRoutingStatus.Accepted, router.Route(message));

            route.TryRoute(message).Returns(false);
            Assert.Equal(MessageRoutingStatus.QueueFull, router.Route(message));

            Assert.True(router.Unregister("game-1"));
            Assert.Equal(MessageRoutingStatus.GameNotFound, router.Route(message));
        }

        [Fact]
        public void GameReceiverRegistry_SetOverwriteRemoveIfSameTryGetAndGameIds_WorkCorrectly()
        {
            var registry = new GameReceiverRegistry();
            var first = Substitute.For<IGameCommandSending>();
            var second = Substitute.For<IGameCommandSending>();

            Assert.Throws<ArgumentException>(() => registry.Set("", first));
            Assert.Throws<ArgumentNullException>(() => registry.Set("g1", null!));
            Assert.Throws<ArgumentException>(() => registry.TryGet("", out _));
            Assert.Throws<ArgumentException>(() => registry.RemoveIfSame("", first));
            Assert.Throws<ArgumentNullException>(() => registry.RemoveIfSame("g1", null!));

            Assert.False(registry.TryGet("g1", out var absent));
            Assert.Null(absent);
            Assert.False(registry.RemoveIfSame("g1", first));

            registry.Set("g1", first);
            Assert.True(registry.TryGet("g1", out var resolvedFirst));
            Assert.Same(first, resolvedFirst);
            Assert.Equal(new[] { "g1" }, registry.GameIds);

            registry.Set("g1", second);
            Assert.True(registry.TryGet("g1", out var resolvedSecond));
            Assert.Same(second, resolvedSecond);

            Assert.False(registry.RemoveIfSame("g1", first));
            Assert.True(registry.TryGet("g1", out var stillSecond));
            Assert.Same(second, stillSecond);

            Assert.True(registry.RemoveIfSame("g1", second));
            Assert.False(registry.TryGet("g1", out _));
            Assert.Empty(registry.GameIds);
        }

        [Fact]
        public void ThreadGameEndpointRoute_TryRoute_ForwardsAndValidatesNulls()
        {
            var delivered = new List<ICommand>();
            var createdCommand = Substitute.For<ICommand>();
            var message = new CommandDescription { GameId = "g1", Type = "Commands.Move" };

            Assert.Throws<ArgumentNullException>(() => new ThreadGameEndpointRoute(null!, _ => createdCommand));
            Assert.Throws<ArgumentNullException>(() => new ThreadGameEndpointRoute(_ => true, null!));

            var acceptingRoute = new ThreadGameEndpointRoute(
                cmd =>
                {
                    delivered.Add(cmd);
                    return true;
                },
                _ => createdCommand);

            Assert.Throws<ArgumentNullException>(() => acceptingRoute.TryRoute(null!));
            Assert.True(acceptingRoute.TryRoute(message));
            Assert.Single(delivered, createdCommand);

            var rejectingRoute = new ThreadGameEndpointRoute(_ => false, _ => createdCommand);
            Assert.False(rejectingRoute.TryRoute(message));
        }

        [Fact]
        public void DeliverMessageCommand_DispatchesPolymorphicallyAndHandlesMissingGameId()
        {
            var registry = new GameReceiverRegistry();
            var message = new CommandDescription { GameId = "g1", Type = "Commands.Move" };

            Assert.Throws<ArgumentNullException>(() => new DeliverMessageCommand(null!, message));
            Assert.Throws<ArgumentNullException>(() => new DeliverMessageCommand(registry, null!));

            new DeliverMessageCommand(registry, message).Execute();

            var proxyStub = Substitute.For<IGameCommandSending>();
            registry.Set("g1", proxyStub);

            new DeliverMessageCommand(registry, message).Execute();
            proxyStub.Received(1).Send(message);
        }

        [Fact]
        public void LocalEndpointGameSending_EnqueuesIntoGameActivatesSchedulerAndSupportsJournal()
        {
            var game = new Game();
            var scheduler = Substitute.For<IGameScheduler>();
            var gameTurn = Substitute.For<ICommand>();
            var schedulingReceiver = new SchedulingCommandReceiver(game, scheduler, gameTurn);
            var journal = new PendingCommandJournal();
            var executed = false;

            Assert.Throws<ArgumentNullException>(() => new LocalEndpointGameSending(null!, _ => Substitute.For<ICommand>()));
            Assert.Throws<ArgumentNullException>(() => new LocalEndpointGameSending(schedulingReceiver, null!));

            var sending = new LocalEndpointGameSending(
                schedulingReceiver,
                desc =>
                {
                    journal.Add(desc);
                    return new DelegatingCommand(() => executed = true);
                });

            Assert.Throws<ArgumentNullException>(() => sending.Send(null!));

            var registry = new GameReceiverRegistry();
            registry.Set("g1", sending);

            var message = new CommandDescription { Id = "c1", GameId = "g1", Type = "Commands.Move" };
            new DeliverMessageCommand(registry, message).Execute();

            scheduler.Received(1).MakeReady(gameTurn);
            Assert.Single(journal.Pending);
            Assert.Same(message, journal.Pending[0]);

            var gameScope = Ioc.Resolve<object>("IoC.Scope.Create", _rootScope);
            new InSeparateScopeCommand(
                gameScope,
                new DelegatingCommand(() =>
                {
                    var remaining = 1;
                    Ioc.Resolve<ICommand>(
                        "IoC.Register",
                        "Game.Loop.CanContinue",
                        (Func<object[], object>)(_ => remaining-- > 0)).Execute();
                    game.Execute();
                })).Execute();

            Assert.True(executed);
        }

        [Fact]
        public void RegisterIoCDependencyEndpoint_RegistersAllHostKeys()
        {
            var serverScope = Ioc.Resolve<object>("IoC.Scope.Create", _rootScope);
            new InSeparateScopeCommand(
                serverScope,
                new DelegatingCommand(() =>
                {
                    new RegisterIoCDependencyEndpoint().Execute();

                    var router = Ioc.Resolve<IGameRouter>("Server.Router");
                    Assert.IsType<GameRouter>(router);

                    var isPaused = Ioc.Resolve<Func<bool>>("Server.Ingress.Paused");
                    Assert.False(isPaused());
                    Ioc.Resolve<ICommand>("Server.Ingress.Paused", true).Execute();
                    Assert.True(isPaused());
                    Ioc.Resolve<ICommand>("Server.Ingress.Paused", false).Execute();
                    Assert.False(isPaused());

                    var decoder = Ioc.Resolve<ICommandMessageDecoding>("Endpoint.Message.Decoder");
                    Assert.IsType<CommandMessageFactory>(decoder);

                    var threadInbox = new Queue<ICommand>();
                    var threadReceiver = new DelegatingCommandReceiver(threadInbox.Enqueue);
                    var tryEnqueue = Ioc.Resolve<Func<ICommand, bool>>("Server.Thread.TryEnqueue", threadReceiver);
                    var registry = new GameReceiverRegistry();
                    var route = Ioc.Resolve<IGameEndpointRoute>("Endpoint.Route.Create", tryEnqueue, registry);
                    router.Register("g1", route);

                    var interpreted = new List<CommandDescription>();
                    Ioc.Resolve<ICommand>(
                        "IoC.Register",
                        "Commands.InterpretMessage",
                        (Func<object[], object>)(args => new DelegatingCommand(() => interpreted.Add((CommandDescription)args[0])))).Execute();

                    var gameQueue = new Queue<ICommand>();
                    var gameReceiver = new DelegatingCommandReceiver(gameQueue.Enqueue);
                    var defaultTarget = Ioc.Resolve<IGameCommandSending>("Endpoint.Target.Local.Create", gameReceiver);
                    registry.Set("g1", defaultTarget);

                    var msg1 = new CommandDescription { GameId = "g1", Type = "Commands.Move" };
                    Assert.Equal(MessageRoutingStatus.Accepted, router.Route(msg1));
                    Assert.Single(threadInbox);
                    threadInbox.Dequeue().Execute();
                    Assert.Single(gameQueue);
                    gameQueue.Dequeue().Execute();
                    Assert.Single(interpreted, msg1);

                    var customCaptured = new List<CommandDescription>();
                    var customTarget = Ioc.Resolve<IGameCommandSending>(
                        "Endpoint.Target.Local.Create",
                        gameReceiver,
                        (Func<CommandDescription, ICommand>)(desc => new DelegatingCommand(() => customCaptured.Add(desc))));
                    registry.Set("g1", customTarget);

                    var msg2 = new CommandDescription { GameId = "g1", Type = "Commands.Rotate" };
                    Assert.Equal(MessageRoutingStatus.Accepted, router.Route(msg2));
                    threadInbox.Dequeue().Execute();
                    gameQueue.Dequeue().Execute();
                    Assert.Single(customCaptured, msg2);
                })).Execute();
        }
    }
}
