using System.Collections.Concurrent;
using App;
using App.Scopes;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Test
{
    public class ServerThreadTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private readonly List<string> _log = new List<string>();
        private readonly List<object[]> _handled = new List<object[]>();
        private readonly BlockingCollection<ICommand> _inbox = new BlockingCollection<ICommand>();
        private readonly RoundRobinGameScheduler _scheduler = new RoundRobinGameScheduler();
        private readonly ServerThreadState _state;
        private readonly ServerThreadLoopCommand _loop;

        public ServerThreadTests()
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

            _state = new ServerThreadState(_inbox, _scheduler);
            _loop = new ServerThreadLoopCommand(_inbox, _scheduler, _state);
        }

        private TestActionCommand Log(string entry)
        {
            return new TestActionCommand(() => _log.Add(entry));
        }

        [Fact]
        public void Loop_HardStop_SkipsRemainingInboxCommands()
        {
            _inbox.Add(Log("first"));
            _inbox.Add(new HardStopServerThreadCommand(_state));
            _inbox.Add(Log("second"));

            _loop.Execute();

            Assert.Equal(new[] { "first" }, _log);
        }

        [Fact]
        public void Loop_SoftStop_DrainsInboxBeforeStopping()
        {
            _inbox.Add(new SoftStopServerThreadCommand(_state));
            _inbox.Add(Log("first"));
            _inbox.Add(Log("second"));

            _loop.Execute();

            Assert.Equal(new[] { "first", "second" }, _log);
        }

        [Fact]
        public void Loop_CommandThrows_RoutesToExceptionHandlerAndContinues()
        {
            var exception = new InvalidOperationException();
            var failing = new TestActionCommand(() => throw exception);
            _inbox.Add(failing);
            _inbox.Add(Log("next"));
            _inbox.Add(new SoftStopServerThreadCommand(_state));

            _loop.Execute();

            var handled = Assert.Single(_handled);
            Assert.Same(failing, handled[0]);
            Assert.Same(exception, handled[1]);
            Assert.Equal(new[] { "next" }, _log);
        }

        [Fact]
        public void Loop_SoftStop_KeepsRunningReadyGamesUntilNoneIsReady()
        {
            var turns = 0;
            ICommand? game = null;
            game = new TestActionCommand(() =>
            {
                turns++;
                _log.Add("game");

                if (turns < 3)
                {
                    _scheduler.MakeReady(game!);
                }
            });
            _scheduler.MakeReady(game);
            _inbox.Add(new SoftStopServerThreadCommand(_state));

            _loop.Execute();

            Assert.Equal(new[] { "game", "game", "game" }, _log);
        }

        [Fact]
        public void Loop_WithReadyGame_AlternatesInboxCommandsAndGameTurns()
        {
            ICommand? game = null;
            game = new TestActionCommand(() =>
            {
                _log.Add("game");
                _scheduler.MakeReady(game!);
            });
            _scheduler.MakeReady(game);
            _inbox.Add(Log("message 1"));
            _inbox.Add(Log("message 2"));
            _inbox.Add(new HardStopServerThreadCommand(_state));

            _loop.Execute();

            Assert.Equal(new[] { "message 1", "game", "message 2", "game" }, _log);
        }

        [Fact]
        public void Thread_ExecutesReceivedCommandsOnItsOwnThread_AndSoftStops()
        {
            var executedOn = 0;
            var thread = Ioc.Resolve<ServerThread>("Server.Thread.Create");
            thread.Receive(new TestActionCommand(() => executedOn = Environment.CurrentManagedThreadId));
            Ioc.Resolve<ICommand>("Server.Thread.Stop.Soft", thread).Execute();

            thread.Start();

            Assert.True(thread.Join(Timeout));
            Assert.NotEqual(0, executedOn);
            Assert.NotEqual(Environment.CurrentManagedThreadId, executedOn);
        }

        [Fact]
        public void Thread_HardStop_SkipsCommandsReceivedAfterIt()
        {
            var thread = Ioc.Resolve<ServerThread>("Server.Thread.Create");
            thread.Receive(Log("before"));
            Ioc.Resolve<ICommand>("Server.Thread.Stop.Hard", thread).Execute();
            thread.Receive(Log("after"));

            thread.Start();

            Assert.True(thread.Join(Timeout));
            Assert.Equal(new[] { "before" }, _log);
        }

        [Fact]
        public void Thread_ResolvesDependenciesFromTheScopeItWasCreatedIn()
        {
            var failing = new TestActionCommand(() => throw new InvalidOperationException());
            var thread = Ioc.Resolve<ServerThread>("Server.Thread.Create");
            thread.Receive(failing);
            Ioc.Resolve<ICommand>("Server.Thread.Stop.Soft", thread).Execute();

            thread.Start();

            Assert.True(thread.Join(Timeout));
            Assert.Same(failing, Assert.Single(_handled)[0]);
        }
    }
}
