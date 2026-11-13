using System;
using App;
using App.Scopes;
using NSubstitute;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Test
{
    public class InSeparateScopeCommandTests
    {
        private readonly object _parentScope;
        private readonly object _targetScope;

        public InSeparateScopeCommandTests()
        {
            new InitCommand().Execute();
            _parentScope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", _parentScope).Execute();
            _targetScope = Ioc.Resolve<object>("IoC.Scope.Create");
        }

        [Fact]
        public void Constructor_WithNullTargetScope_ThrowsArgumentNullException()
        {
            var innerCommand = Substitute.For<ICommand>();

            Assert.Throws<ArgumentNullException>(() => new InSeparateScopeCommand(null!, innerCommand));
        }

        [Fact]
        public void Constructor_WithNullInnerCommand_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new InSeparateScopeCommand(_targetScope, null!));
        }

        [Fact]
        public void Execute_SwitchesToTargetScope_ExecutesInnerCommand_AndRestoresPreviousScope()
        {
            object? scopeDuringExecution = null;
            var innerCommand = Substitute.For<ICommand>();
            innerCommand.When(c => c.Execute()).Do(_ =>
            {
                scopeDuringExecution = Ioc.Resolve<object>("IoC.Scope.Current");
            });

            var command = new InSeparateScopeCommand(_targetScope, innerCommand);

            command.Execute();

            innerCommand.Received(1).Execute();
            Assert.Same(_targetScope, scopeDuringExecution);
            Assert.Same(_parentScope, Ioc.Resolve<object>("IoC.Scope.Current"));
        }

        [Fact]
        public void Execute_WhenInnerCommandThrows_PropagatesException_AndRestoresPreviousScope()
        {
            var innerCommand = Substitute.For<ICommand>();
            innerCommand.When(c => c.Execute()).Do(_ => throw new InvalidOperationException("boom"));

            var command = new InSeparateScopeCommand(_targetScope, innerCommand);

            Assert.Throws<InvalidOperationException>(() => command.Execute());
            Assert.Same(_parentScope, Ioc.Resolve<object>("IoC.Scope.Current"));
        }

        [Fact]
        public void Execute_WithEmptyTargetScope_RestoresPreviousScopeWithoutResolvingInsideTargetScope()
        {
            var emptyScope = Ioc.Resolve<object>("IoC.Scope.Create.Empty");
            var innerCommand = Substitute.For<ICommand>();

            var command = new InSeparateScopeCommand(emptyScope, innerCommand);

            command.Execute();

            innerCommand.Received(1).Execute();
            Assert.Same(_parentScope, Ioc.Resolve<object>("IoC.Scope.Current"));
        }
    }
}
