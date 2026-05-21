using System;
using NSubstitute;
using Xunit;
using App;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests
{
    public class CommandInjectableCommandTests
    {
        [Fact]
        public void Execute_WhenCommandInjected_ShouldCallInjectedCommand()
        {
            var commandInjectable = new CommandInjectableCommand();
            var mockInjectedCommand = Substitute.For<ICommand>();

            commandInjectable.Inject(mockInjectedCommand);
            commandInjectable.Execute();

            mockInjectedCommand.Received(1).Execute();
        }

        [Fact]
        public void Execute_WhenNoCommandInjected_ShouldThrowException()
        {
            var commandInjectable = new CommandInjectableCommand();

            var exception = Assert.Throws<InvalidOperationException>(() => commandInjectable.Execute());

            Assert.Equal("", exception.Message);
        }

        [Fact]
        public void Inject_ShouldStoreCommand()
        {
            var commandInjectable = new CommandInjectableCommand();
            var mockInjectedCommand = Substitute.For<ICommand>();

            commandInjectable.Inject(mockInjectedCommand);

            var exception = Record.Exception(() => commandInjectable.Execute());
            Assert.Null(exception);
            mockInjectedCommand.Received(1).Execute();
        }
    }
}
