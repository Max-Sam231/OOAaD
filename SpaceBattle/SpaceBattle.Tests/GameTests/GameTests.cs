using System;
using Xunit;
using NSubstitute;
using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Test
{
    public class GameTests
    {
        private readonly object _testGameScope;

        public GameTests()
        {
            new InitCommand().Execute();
            _testGameScope = Ioc.Resolve<object>("IoC.Scope.Create");
        }

        [Fact]
        public void Game_ShouldProcessCommandSuccessfully_AndExitLoop()
        {
            var game = new Game(_testGameScope);
            var mockCommand = Substitute.For<ICommand>();

            bool canContinue = true;
            mockCommand.When(x => x.Execute()).Do(x =>
            {
                canContinue = false;
            });

            var previousScope = Ioc.Resolve<object>("IoC.Scope.Current");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", _testGameScope).Execute();
            Ioc.Resolve<ICommand>("IoC.Register", "Game.Loop.CanContinue", (Func<object[], object>)(args => canContinue)).Execute();
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", previousScope).Execute();

            game.Receive(mockCommand);
            game.Execute();
            mockCommand.Received(1).Execute();
        }

        [Fact]
        public void Game_WhenCommandThrowsException_ShouldNotBreakLoopAndProcessNextCommands()
        {
            var game = new Game(_testGameScope);

            var badCommand = Substitute.For<ICommand>();
            badCommand.When(x => x.Execute()).Do(x => throw new Exception("Критическая ошибка в игре"));

            bool canContinue = true;
            var stopCommand = Substitute.For<ICommand>();
            stopCommand.When(x => x.Execute()).Do(x =>
            {
                canContinue = false;
            });

            var previousScope = Ioc.Resolve<object>("IoC.Scope.Current");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", _testGameScope).Execute();
            Ioc.Resolve<ICommand>("IoC.Register", "Game.Loop.CanContinue", (Func<object[], object>)(args => canContinue)).Execute();
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", previousScope).Execute();

            game.Receive(badCommand);
            game.Receive(stopCommand);
            game.Execute();
            badCommand.Received(1).Execute();
            stopCommand.Received(1).Execute();
        }

        [Fact]
        public void Game_WhenQueueIsEmpty_ShouldExecuteRealServerIdleCommandWithSleep_AndExit()
        {
            var game = new Game(_testGameScope);

            var previousScope = Ioc.Resolve<object>("IoC.Scope.Current");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", _testGameScope).Execute();

            Ioc.Resolve<ICommand>("IoC.Register", "Game.Loop.Idle", (Func<object[], object>)(args => new ServerIdleCommand())).Execute();

            int callCount = 0;
            Ioc.Resolve<ICommand>("IoC.Register", "Game.Loop.CanContinue", (Func<object[], object>)(args =>
            {
                callCount++;
                return callCount <= 1;
            })).Execute();

            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", previousScope).Execute();
            game.Execute();
            Assert.Equal(2, callCount);
        }
        [Fact]
        public void Game_Constructor_WithNullScope_ShouldThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new Game(null!));
        }

        [Fact]
        public void Game_Receive_WithNullCommand_ShouldIgnoreAndNotEnqueue()
        {
            var game = new Game(_testGameScope);

            var previousScope = Ioc.Resolve<object>("IoC.Scope.Current");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", _testGameScope).Execute();

            Ioc.Resolve<ICommand>("IoC.Register", "Game.Loop.CanContinue", (Func<object[], object>)(args => false)).Execute();

            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", previousScope).Execute();

            game.Receive(null!);
            game.Execute();
        }
    }
}