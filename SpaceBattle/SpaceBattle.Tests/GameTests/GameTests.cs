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
            var game = new Game();
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
            new InSeparateScopeCommand(_testGameScope, game).Execute();
            mockCommand.Received(1).Execute();
        }

        [Fact]
        public void Game_WhenCommandThrowsException_ShouldRouteToExceptionHandlerAndProcessNextCommands()
        {
            var game = new Game();
            var expectedException = new Exception("Game loop test exception");

            var badCommand = Substitute.For<ICommand>();
            badCommand.When(x => x.Execute()).Do(x => throw expectedException);

            var handlerCommand = Substitute.For<ICommand>();
            ICommand? handledCommand = null;
            Exception? handledException = null;

            bool canContinue = true;
            var stopCommand = Substitute.For<ICommand>();
            stopCommand.When(x => x.Execute()).Do(x =>
            {
                canContinue = false;
            });

            var previousScope = Ioc.Resolve<object>("IoC.Scope.Current");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", _testGameScope).Execute();
            Ioc.Resolve<ICommand>("IoC.Register", "Game.Loop.CanContinue", (Func<object[], object>)(args => canContinue)).Execute();
            Ioc.Resolve<ICommand>("IoC.Register", "Exception.Handle", (Func<object[], object>)(args =>
            {
                handledCommand = (ICommand)args[0];
                handledException = (Exception)args[1];
                return handlerCommand;
            })).Execute();
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", previousScope).Execute();

            game.Receive(badCommand);
            game.Receive(stopCommand);
            new InSeparateScopeCommand(_testGameScope, game).Execute();

            badCommand.Received(1).Execute();
            handlerCommand.Received(1).Execute();
            Assert.Same(badCommand, handledCommand);
            Assert.Same(expectedException, handledException);
            stopCommand.Received(1).Execute();
        }

        [Fact]
        public void Game_WhenQueueIsEmpty_ShouldExecuteRealServerIdleCommandWithSleep_AndExit()
        {
            var game = new Game();

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
            new InSeparateScopeCommand(_testGameScope, game).Execute();
            Assert.Equal(2, callCount);
        }

        [Fact]
        public void Game_Receive_WithNullCommand_ShouldIgnoreAndNotEnqueue()
        {
            var game = new Game();
            var idleCommand = Substitute.For<ICommand>();
            var exceptionHandlerCommand = Substitute.For<ICommand>();
            int callCount = 0;

            var previousScope = Ioc.Resolve<object>("IoC.Scope.Current");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", _testGameScope).Execute();

            Ioc.Resolve<ICommand>("IoC.Register", "Game.Loop.Idle", (Func<object[], object>)(args => idleCommand)).Execute();
            Ioc.Resolve<ICommand>("IoC.Register", "Exception.Handle", (Func<object[], object>)(args => exceptionHandlerCommand)).Execute();
            Ioc.Resolve<ICommand>("IoC.Register", "Game.Loop.CanContinue", (Func<object[], object>)(args =>
            {
                callCount++;
                return callCount <= 1;
            })).Execute();

            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", previousScope).Execute();

            game.Receive(null!);
            new InSeparateScopeCommand(_testGameScope, game).Execute();

            Assert.Equal(2, callCount);
            idleCommand.Received(1).Execute();
            exceptionHandlerCommand.DidNotReceive().Execute();
        }
    }
}