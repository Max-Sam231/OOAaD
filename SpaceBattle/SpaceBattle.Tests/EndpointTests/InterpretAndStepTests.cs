using System.Text.Json;
using App;
using App.Scopes;
using NSubstitute;
using SpaceBattle.Lib;

namespace SpaceBattle.Test
{
    public class InterpretAndStepTests : IDisposable
    {
        private readonly object _rootScope;

        public InterpretAndStepTests()
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
        public void GameStepState_StartsAtZeroAdvancesRestoresAndValidatesNegatives()
        {
            var state = new GameStepState();
            Assert.Equal(0L, state.CurrentStep);
            Assert.Equal(0L, state.MaxFutureDelta);

            state.Advance();
            Assert.Equal(1L, state.CurrentStep);

            state.Restore(10);
            Assert.Equal(10L, state.CurrentStep);

            var withDelta = new GameStepState(3);
            Assert.Equal(3L, withDelta.MaxFutureDelta);

            Assert.Throws<ArgumentOutOfRangeException>(() => new GameStepState(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Restore(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RegisterIoCDependencyGameStep(-1));
        }

        [Fact]
        public void NonBlockingStepValidating_ValidatesBoundsAndNulls()
        {
            Assert.Throws<ArgumentNullException>(() => new NonBlockingStepValidating(null!));

            var state = new GameStepState(2);
            state.Restore(5);
            var validator = new NonBlockingStepValidating(state);

            Assert.Throws<ArgumentNullException>(() => validator.Validate(null!));
            Assert.False(validator.Validate(new CommandDescription { Step = 4 }));
            Assert.True(validator.Validate(new CommandDescription { Step = 5 }));
            Assert.True(validator.Validate(new CommandDescription { Step = 7 }));
            Assert.False(validator.Validate(new CommandDescription { Step = 8 }));
        }

        [Fact]
        public void GameConnectionRegistry_SubscribePublishUnsubscribeAndCount_WorkCorrectly()
        {
            var registry = new GameConnectionRegistry();
            var sender1 = Substitute.For<IClientMessageSending>();
            var sender2 = Substitute.For<IClientMessageSending>();
            var payload = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3 });

            Assert.Throws<ArgumentException>(() => registry.Subscribe("", "c1", sender1));
            Assert.Throws<ArgumentException>(() => registry.Subscribe("g1", "", sender1));
            Assert.Throws<ArgumentNullException>(() => registry.Subscribe("g1", "c1", null!));
            Assert.Throws<ArgumentException>(() => registry.Unsubscribe("", "c1"));
            Assert.Throws<ArgumentException>(() => registry.Unsubscribe("g1", ""));
            Assert.Throws<ArgumentException>(() => registry.Publish("", payload));
            Assert.Throws<ArgumentException>(() => registry.GetConnectionCount(""));

            Assert.Equal(0, registry.GetConnectionCount("g1"));
            registry.Publish("g1", payload);
            registry.Unsubscribe("g1", "c1");

            registry.Subscribe("g1", "c1", sender1);
            registry.Subscribe("g1", "c2", sender2);
            Assert.Equal(2, registry.GetConnectionCount("g1"));

            registry.Publish("g1", payload);
            sender1.Received(1).TrySend(payload);
            sender2.Received(1).TrySend(payload);

            registry.Unsubscribe("g1", "c1");
            Assert.Equal(1, registry.GetConnectionCount("g1"));

            registry.Unsubscribe("g1", "c2");
            Assert.Equal(0, registry.GetConnectionCount("g1"));

            registry.Publish("g1", payload);
            sender2.Received(1).TrySend(payload);
        }

        [Fact]
        public void BroadcastGameStateCommand_FastPathsWhenNoConnectionsAndPublishesWhenSubscribed()
        {
            Assert.Throws<ArgumentException>(() => new BroadcastGameStateCommand(""));

            var connections = new GameConnectionRegistry();
            var stepResolved = false;
            var serializeResolved = false;

            var scope = Ioc.Resolve<object>("IoC.Scope.Create", _rootScope);
            new InSeparateScopeCommand(
                scope,
                new DelegatingCommand(() =>
                {
                    Ioc.Resolve<ICommand>(
                        "IoC.Register",
                        "Server.Connections",
                        (Func<object[], object>)(_ => connections)).Execute();
                    Ioc.Resolve<ICommand>(
                        "IoC.Register",
                        "Game.Step.Current",
                        (Func<object[], object>)(_ =>
                        {
                            stepResolved = true;
                            return 4L;
                        })).Execute();
                    Ioc.Resolve<ICommand>(
                        "IoC.Register",
                        "Game.State.Serialize",
                        (Func<object[], object>)(args =>
                        {
                            serializeResolved = true;
                            return new ReadOnlyMemory<byte>(new byte[] { 42 });
                        })).Execute();

                    new BroadcastGameStateCommand("g1").Execute();
                    Assert.False(stepResolved);
                    Assert.False(serializeResolved);

                    var received = new List<ReadOnlyMemory<byte>>();
                    var sender = Substitute.For<IClientMessageSending>();
                    sender.TrySend(Arg.Do<ReadOnlyMemory<byte>>(p => received.Add(p))).Returns(true);
                    connections.Subscribe("g1", "c1", sender);

                    new BroadcastGameStateCommand("g1").Execute();
                    Assert.True(stepResolved);
                    Assert.True(serializeResolved);
                    Assert.Single(received);
                    Assert.Equal(new byte[] { 42 }, received[0].ToArray());
                })).Execute();
        }

        [Fact]
        public void InterpretMessageCommand_InFourLevelHierarchy_CoversStepArityAuthBroadcastAndExceptions()
        {
            var connections = new GameConnectionRegistry();
            var exceptions = new List<(ICommand Command, Exception Exception)>();

            Assert.Throws<ArgumentNullException>(() =>
                new RegisterIoCDependencyEndpointGameBase(null!, (_, _) => { }));
            Assert.Throws<ArgumentNullException>(() =>
                new RegisterIoCDependencyEndpointGameBase(connections, null!));
            Assert.Throws<ArgumentNullException>(() => new InterpretMessageCommand(null!));

            var serverScope = Ioc.Resolve<object>("IoC.Scope.Create", _rootScope);
            var baseInitializer = new DelegatingCommand(() =>
            {
                new RegisterIoCDependencySnapshotCodecs().Execute();
                new RegisterIoCDependencyAuthorizeCommand().Execute();
                new RegisterIoCDependencyGameRules().Execute();
                new RegisterIoCDependencyEndpointGameBase(
                    connections,
                    (cmd, ex) => exceptions.Add((cmd, ex))).Execute();
            });

            object gameScope = null!;
            new InSeparateScopeCommand(
                serverScope,
                new DelegatingCommand(() =>
                {
                    new RegisterIoCDependencyScopeHierarchy(_rootScope, baseInitializer).Execute();
                    gameScope = Ioc.Resolve<object>("Scopes.Game.Create");
                })).Execute();

            var repo = new EnumerableGameObjectRepository();
            var ship = new Dictionary<string, object>
            {
                ["Position"] = new Vector(1, 2),
                ["OwnerId"] = "player-1",
            };
            var asteroid = new Dictionary<string, object>
            {
                ["Position"] = new Vector(5, 5),
            };
            var malformedOwner = new Dictionary<string, object>
            {
                ["OwnerId"] = 123,
            };
            repo.Set(1, ship);
            repo.Set(2, asteroid);
            repo.Set(3, malformedOwner);

            var moveCalls = 0;
            var configuredSpeed = 0;

            new InSeparateScopeCommand(
                gameScope,
                new DelegatingCommand(() =>
                {
                    new RegisterIoCDependencyGameStep().Execute();
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
                            return new DelegatingCommand(() =>
                            {
                                moveCalls++;
                                item["Position"] = new Vector(2, 2);
                            });
                        })).Execute();
                    Ioc.Resolve<ICommand>(
                        "IoC.Register",
                        "Commands.SetSpeed",
                        (Func<object[], object>)(args =>
                        {
                            var item = (IDictionary<string, object>)args[0];
                            var cmdArgs = (CommandArguments)args[1];
                            return new DelegatingCommand(() =>
                            {
                                configuredSpeed = cmdArgs.Get<int>("speed");
                                item["Speed"] = configuredSpeed;
                            });
                        })).Execute();

                    Assert.Equal("player-1", Ioc.Resolve<string>("Game.Item.Owner", ship));
                    Assert.Equal(string.Empty, Ioc.Resolve<string>("Game.Item.Owner", asteroid));
                    Assert.Equal(string.Empty, Ioc.Resolve<string>("Game.Item.Owner", malformedOwner));

                    var stepState = Ioc.Resolve<GameStepState>("Game.Step.State");
                    Assert.Equal(0L, stepState.CurrentStep);
                    Assert.Equal(0L, Ioc.Resolve<long>("Game.Step.Current"));

                    var pushed = new List<ReadOnlyMemory<byte>>();
                    var client = Substitute.For<IClientMessageSending>();
                    client.TrySend(Arg.Do<ReadOnlyMemory<byte>>(p => pushed.Add(p))).Returns(true);
                    connections.Subscribe("game-1", "conn-1", client);

                    Ioc.Resolve<ICommand>(
                        "Commands.InterpretMessage",
                        new CommandDescription
                        {
                            GameId = "game-1",
                            PlayerId = "",
                            Type = "Commands.Move",
                            GameItemId = 1,
                            Step = 5,
                        }).Execute();
                    Assert.Equal(0, moveCalls);
                    Assert.Equal(0L, Ioc.Resolve<long>("Game.Step.Current"));
                    Assert.Empty(pushed);

                    Ioc.Resolve<ICommand>(
                        "Commands.InterpretMessage",
                        new CommandDescription
                        {
                            GameId = "game-1",
                            PlayerId = "",
                            Type = "Commands.Move",
                            GameItemId = 1,
                            Step = 0,
                        }).Execute();
                    Assert.Equal(1, moveCalls);
                    Assert.Equal(1L, Ioc.Resolve<long>("Game.Step.Current"));
                    Assert.Single(pushed);
                    using (var doc = JsonDocument.Parse(pushed[0]))
                    {
                        Assert.Equal("state", doc.RootElement.GetProperty("kind").GetString());
                        Assert.Equal("game-1", doc.RootElement.GetProperty("game_id").GetString());
                        Assert.Equal(1L, doc.RootElement.GetProperty("step").GetInt64());
                        Assert.Equal("running", doc.RootElement.GetProperty("status").GetString());
                    }

                    Ioc.Resolve<ICommand>(
                        "Commands.InterpretMessage",
                        new CommandDescription
                        {
                            GameId = "game-1",
                            PlayerId = "player-1",
                            Type = "Commands.SetSpeed",
                            GameItemId = 1,
                            Step = 1,
                            Args = new Dictionary<string, Dictionary<string, JsonElement>>
                            {
                                ["speed"] = new Dictionary<string, JsonElement>
                                {
                                    ["type"] = JsonSerializer.SerializeToElement("System.Int32"),
                                    ["value"] = JsonSerializer.SerializeToElement(7),
                                },
                            },
                        }).Execute();
                    Assert.Equal(7, configuredSpeed);
                    Assert.Equal(2L, Ioc.Resolve<long>("Game.Step.Current"));
                    Assert.Equal(2, pushed.Count);

                    var game = new Game();
                    game.Receive(Ioc.Resolve<ICommand>(
                        "Commands.InterpretMessage",
                        new CommandDescription
                        {
                            GameId = "game-1",
                            PlayerId = "player-2",
                            Type = "Commands.Move",
                            GameItemId = 1,
                            Step = 2,
                        }));

                    var turns = 1;
                    Ioc.Resolve<ICommand>(
                        "IoC.Register",
                        "Game.Loop.CanContinue",
                        (Func<object[], object>)(_ => turns-- > 0)).Execute();
                    game.Execute();

                    Assert.Equal(1, moveCalls);
                    Assert.Equal(2L, Ioc.Resolve<long>("Game.Step.Current"));
                    Assert.Single(exceptions);
                    Assert.IsType<InvalidOperationException>(exceptions[0].Exception);

                    Ioc.Resolve<ICommand>(
                        "Commands.InterpretMessage",
                        new CommandDescription
                        {
                            GameId = "game-1",
                            PlayerId = null!,
                            Type = "Commands.Move",
                            GameItemId = 1,
                            Step = 2,
                        }).Execute();
                    Assert.Equal(2, moveCalls);
                    Assert.Equal(3L, Ioc.Resolve<long>("Game.Step.Current"));
                })).Execute();
        }
    }
}
