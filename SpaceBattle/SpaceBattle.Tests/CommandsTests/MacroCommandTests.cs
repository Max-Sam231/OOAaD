using System;
using SpaceBattle.Lib;
using NSubstitute;

namespace SpaceBattle.Lib.Tests.CommandsTests
{
    public class MacroCommandTests
    {
        [Fact]
        public void Execute_AllCommandsExecuted()
        {
            var command1 = Substitute.For<ICommand>();
            var command2 = Substitute.For<ICommand>();

            var macro = new MacroCommand(new[] { command1, command2 });

            macro.Execute();

            command1.Received(1).Execute();
            command2.Received(1).Execute();
        }

        [Fact]
        public void Execute_WhenCommandThrowsException_DoesNotExecuteRemainingCommands()
        {
            var command1 = Substitute.For<ICommand>();
            var command2 = Substitute.For<ICommand>();
            var command3 = Substitute.For<ICommand>();

            command2.When(x => x.Execute()).Do(x => { throw new Exception("Error"); });

            var macro = new MacroCommand(new[] { command1, command2, command3 });

            Assert.Throws<Exception>(() => macro.Execute());

            command1.Received(1).Execute();
            command2.Received(1).Execute();
            command3.DidNotReceive().Execute();
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenCommandsAreNull()
        {
            Assert.Throws<ArgumentNullException>(() => new MacroCommand(null!));
        }

        public void Execute_DoesNothing_WhenCommandArrayIsEmpty()
        {
            var macro = new MacroCommand(Array.Empty<ICommand>());

            macro.Execute();
        }
    }
}
