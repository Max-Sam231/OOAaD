using System.Collections.Concurrent;
using App;
using App.Scopes;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Test
{
    public class GameCreateTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private readonly List<string> _log = new List<string>();
        private readonly List<object[]> _handled = new List<object[]>();
        private readonly ServerThread _thread;

        public GameCreateTests()
        {
            new InitCommand().Execute();

            var scope = Ioc.Resolve<object>("IoC.Scope.Create");

            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", scope).Execute();

            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Exception.Handle",
                (Func<object[], object>)(args =>
                {
                    _handled.Add(args);

                    return new TestActionCommand(() => { });
                })
            ).Execute();

            new RegisterIoCDependencyServerThread().Execute();
            new RegisterIoCDependencyGameCreate().Execute();

            _thread = Ioc.Resolve<ServerThread>("Server.Thread.Create");
        }

        private TestActionCommand Log(string entry)
        {
            return new TestActionCommand(() => _log.Add(entry));
        }

        private void SoftStopAndJoin()
        {
            Ioc.Resolve<ICommand>("Server.Thread.Stop.Soft", _thread).Execute();

            Assert.True(_thread.Join(Timeout));
        }

        [Fact]
        public void TwoGames_WithQuantumOfTwo_TakeTurnsInRoundRobinOrder()
        {
            var inbox = new BlockingCollection<ICommand>();
            var scheduler = new RoundRobinGameScheduler();
            var state = new ServerThreadState(inbox, scheduler);
            var gameA = CreateGameOn(scheduler, 2);
            var gameB = CreateGameOn(scheduler, 2);
            Enumerable.Range(0, 5).ToList().ForEach(_ => gameA.Receive(Log("A")));
            Enumerable.Range(0, 5).ToList().ForEach(_ => gameB.Receive(Log("B")));
            inbox.Add(new SoftStopServerThreadCommand(state));

            new ServerThreadLoopCommand(inbox, scheduler, state).Execute();

            Assert.Equal(new[] { "A", "A", "B", "B", "A", "A", "B", "B", "A", "B" }, _log);
        }

        [Fact]
        public void Games_ResolveTheSameKeyFromTheirOwnScopes()
        {
            var gameA = Ioc.Resolve<ICommandReceiver>("Game.Create", _thread, 10);
            var gameB = Ioc.Resolve<ICommandReceiver>("Game.Create", _thread, 10);
            gameA.Receive(RegisterRule("fuel"));
            gameB.Receive(RegisterRule("no fuel"));
            gameA.Receive(new TestActionCommand(() => _log.Add("A: " + Ioc.Resolve<string>("Rule"))));
            gameB.Receive(new TestActionCommand(() => _log.Add("B: " + Ioc.Resolve<string>("Rule"))));

            _thread.Start();
            SoftStopAndJoin();

            Assert.Equal(new[] { "A: fuel", "B: no fuel" }, _log.Order());
        }

        [Fact]
        public void Game_ReceivingFromAnotherThreadWhileServerWaits_ExecutesTheCommand()
        {
            var game = Ioc.Resolve<ICommandReceiver>("Game.Create", _thread, 10);
            using var executed = new ManualResetEventSlim();
            _thread.Start();

            game.Receive(Log("first"));
            game.Receive(new TestActionCommand(executed.Set));

            Assert.True(executed.Wait(Timeout));

            game.Receive(Log("after idle"));
            SoftStopAndJoin();

            Assert.Equal(new[] { "first", "after idle" }, _log);
        }

        [Fact]
        public void Game_CommandThrows_IsRoutedToExceptionHandlerAndGameContinues()
        {
            var failing = new TestActionCommand(() => throw new InvalidOperationException());
            var game = Ioc.Resolve<ICommandReceiver>("Game.Create", _thread, 10);
            game.Receive(failing);
            game.Receive(Log("next"));

            _thread.Start();
            SoftStopAndJoin();

            Assert.Same(failing, Assert.Single(_handled)[0]);
            Assert.Equal(new[] { "next" }, _log);
        }

        [Fact]
        public void DelegatingReceiver_WithNullDelegate_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new DelegatingCommandReceiver(null!));
        }

        private static ICommandReceiver CreateGameOn(IGameScheduler scheduler, int commandLimit)
        {
            var quantum = new GameQuantumState(commandLimit);
            var gameScope = Ioc.Resolve<object>("IoC.Scope.Create");
            new InSeparateScopeCommand(gameScope, new RegisterIoCDependencyGameQuantum(quantum)).Execute();
            var game = new Game();
            var turn = new GameQuantumCommand(new InSeparateScopeCommand(gameScope, game), quantum, scheduler);

            return new SchedulingCommandReceiver(game, scheduler, turn);
        }

        private static ICommand RegisterRule(string rule)
        {
            return new TestActionCommand(() => Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Rule",
                (Func<object[], object>)(args => rule)
            ).Execute());
        }
    }
}
