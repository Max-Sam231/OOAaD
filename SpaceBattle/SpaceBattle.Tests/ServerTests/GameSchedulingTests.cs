using System.Collections.Concurrent;
using App;
using NSubstitute;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Test
{
    public class TestActionCommand : ICommand
    {
        private readonly Action _action;

        public TestActionCommand(Action action)
        {
            _action = action;
        }

        public void Execute()
        {
            _action();
        }
    }

    public class GameSchedulingTests
    {
        [Fact]
        public void Scheduler_WhenEmpty_HasNothingReady()
        {
            var scheduler = new RoundRobinGameScheduler();

            Assert.False(scheduler.HasReady);
            Assert.False(scheduler.TryTakeReady(out _));
        }

        [Fact]
        public void Scheduler_TakesGamesInTheOrderTheyBecameReady()
        {
            var scheduler = new RoundRobinGameScheduler();
            var first = Substitute.For<ICommand>();
            var second = Substitute.For<ICommand>();

            scheduler.MakeReady(first);
            scheduler.MakeReady(second);

            Assert.True(scheduler.TryTakeReady(out var takenFirst));
            Assert.True(scheduler.TryTakeReady(out var takenSecond));
            Assert.Same(first, takenFirst);
            Assert.Same(second, takenSecond);
            Assert.False(scheduler.HasReady);
        }

        [Fact]
        public void Scheduler_GameMadeReadyTwice_IsTakenOnceAndCanBecomeReadyAgain()
        {
            var scheduler = new RoundRobinGameScheduler();
            var game = Substitute.For<ICommand>();

            scheduler.MakeReady(game);
            scheduler.MakeReady(game);

            Assert.True(scheduler.TryTakeReady(out _));
            Assert.False(scheduler.HasReady);

            scheduler.MakeReady(game);

            Assert.True(scheduler.HasReady);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void QuantumState_WithNonPositiveLimit_ThrowsArgumentOutOfRangeException(int limit)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameQuantumState(limit));
        }

        [Fact]
        public void QuantumState_AllowsExactlyLimitCommandsPerTurn()
        {
            var state = new GameQuantumState(2);

            Assert.True(state.CanContinue());
            Assert.True(state.CanContinue());
            Assert.False(state.CanContinue());
            Assert.False(state.IsIdle);
        }

        [Fact]
        public void QuantumState_AfterMarkIdle_StopsUntilStarted()
        {
            var state = new GameQuantumState(2);

            new MarkGameIdleCommand(state).Execute();

            Assert.True(state.IsIdle);
            Assert.False(state.CanContinue());

            state.Start();

            Assert.False(state.IsIdle);
            Assert.True(state.CanContinue());
        }

        [Fact]
        public void QuantumCommand_WhenTurnEndedByLimit_MakesGameReadyAgain()
        {
            var state = new GameQuantumState(1);
            var scheduler = new RoundRobinGameScheduler();
            var game = new TestActionCommand(() => state.CanContinue());
            var turn = new GameQuantumCommand(game, state, scheduler);

            turn.Execute();

            Assert.True(scheduler.TryTakeReady(out var ready));
            Assert.Same(turn, ready);
        }

        [Fact]
        public void QuantumCommand_WhenGameWentIdle_DoesNotMakeItReady()
        {
            var state = new GameQuantumState(1);
            var scheduler = new RoundRobinGameScheduler();
            var turn = new GameQuantumCommand(new TestActionCommand(state.MarkIdle), state, scheduler);

            turn.Execute();

            Assert.False(scheduler.HasReady);
        }

        [Fact]
        public void QuantumCommand_WhenGameThrows_PropagatesAndMakesGameReadyAgain()
        {
            var state = new GameQuantumState(1);
            var scheduler = new RoundRobinGameScheduler();
            var game = new TestActionCommand(() => throw new InvalidOperationException());
            var turn = new GameQuantumCommand(game, state, scheduler);

            Assert.Throws<InvalidOperationException>(turn.Execute);
            Assert.True(scheduler.HasReady);
        }

        [Fact]
        public void SchedulingReceiver_ForwardsCommandAndMakesGameReady()
        {
            var inner = Substitute.For<ICommandReceiver>();
            var scheduler = new RoundRobinGameScheduler();
            var turn = Substitute.For<ICommand>();
            var command = Substitute.For<ICommand>();

            new SchedulingCommandReceiver(inner, scheduler, turn).Receive(command);

            inner.Received(1).Receive(command);
            Assert.True(scheduler.TryTakeReady(out var ready));
            Assert.Same(turn, ready);
        }

        [Fact]
        public void ThreadState_ContinuesUntilHardStop()
        {
            var state = new ServerThreadState(new BlockingCollection<ICommand>(), new RoundRobinGameScheduler());

            Assert.True(state.CanContinue);

            new HardStopServerThreadCommand(state).Execute();

            Assert.False(state.CanContinue);
        }

        [Fact]
        public void ThreadState_AfterSoftStop_ContinuesWhileInboxHasCommands()
        {
            var inbox = new BlockingCollection<ICommand> { Substitute.For<ICommand>() };
            var state = new ServerThreadState(inbox, new RoundRobinGameScheduler());

            new SoftStopServerThreadCommand(state).Execute();

            Assert.True(state.CanContinue);

            inbox.Take();

            Assert.False(state.CanContinue);
        }

        [Fact]
        public void ThreadState_AfterSoftStop_ContinuesWhileGameIsReady()
        {
            var scheduler = new RoundRobinGameScheduler();
            var state = new ServerThreadState(new BlockingCollection<ICommand>(), scheduler);
            scheduler.MakeReady(Substitute.For<ICommand>());

            state.RequestSoftStop();

            Assert.True(state.CanContinue);
        }

        [Fact]
        public void Constructors_WithNullArguments_ThrowArgumentNullException()
        {
            var command = Substitute.For<ICommand>();
            var receiver = Substitute.For<ICommandReceiver>();
            var quantum = new GameQuantumState(1);
            var scheduler = new RoundRobinGameScheduler();
            var inbox = new BlockingCollection<ICommand>();
            var state = new ServerThreadState(inbox, scheduler);
            var thread = new Thread(() => { });

            Assert.Throws<ArgumentNullException>(() => new GameQuantumCommand(null!, quantum, scheduler));
            Assert.Throws<ArgumentNullException>(() => new GameQuantumCommand(command, null!, scheduler));
            Assert.Throws<ArgumentNullException>(() => new GameQuantumCommand(command, quantum, null!));
            Assert.Throws<ArgumentNullException>(() => new MarkGameIdleCommand(null!));
            Assert.Throws<ArgumentNullException>(() => new RegisterIoCDependencyGameQuantum(null!));
            Assert.Throws<ArgumentNullException>(() => new SchedulingCommandReceiver(null!, scheduler, command));
            Assert.Throws<ArgumentNullException>(() => new SchedulingCommandReceiver(receiver, null!, command));
            Assert.Throws<ArgumentNullException>(() => new SchedulingCommandReceiver(receiver, scheduler, null!));
            Assert.Throws<ArgumentNullException>(() => new ServerThreadState(null!, scheduler));
            Assert.Throws<ArgumentNullException>(() => new ServerThreadState(inbox, null!));
            Assert.Throws<ArgumentNullException>(() => new HardStopServerThreadCommand(null!));
            Assert.Throws<ArgumentNullException>(() => new SoftStopServerThreadCommand(null!));
            Assert.Throws<ArgumentNullException>(() => new ServerThreadLoopCommand(null!, scheduler, state));
            Assert.Throws<ArgumentNullException>(() => new ServerThreadLoopCommand(inbox, null!, state));
            Assert.Throws<ArgumentNullException>(() => new ServerThreadLoopCommand(inbox, scheduler, null!));
            Assert.Throws<ArgumentNullException>(() => new ServerThread(null!, inbox, scheduler, state));
            Assert.Throws<ArgumentNullException>(() => new ServerThread(thread, null!, scheduler, state));
            Assert.Throws<ArgumentNullException>(() => new ServerThread(thread, inbox, null!, state));
            Assert.Throws<ArgumentNullException>(() => new ServerThread(thread, inbox, scheduler, null!));
        }
    }
}
