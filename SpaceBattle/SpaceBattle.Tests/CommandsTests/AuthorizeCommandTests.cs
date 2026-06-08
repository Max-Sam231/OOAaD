using App;
using NSubstitute;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Tests
{
    public class AuthorizeCommandTests
    {
        [Fact]
        public void Execute_ShouldExecuteInnerCommand_WhenSubjectMatchesOwner()
        {
            var innerCommand = Substitute.For<ICommand>();
            var subject = "Player1";
            var objectOwner = "Player1";

            var authorizeCommand = new AuthorizeCommand(innerCommand, subject, objectOwner);
            authorizeCommand.Execute();

            innerCommand.Received(1).Execute();
        }

        [Fact]
        public void Execute_ShouldThrowInvalidOperationException_WhenSubjectDoesNotMatchOwner()
        {
            var innerCommand = Substitute.For<ICommand>();
            var subject = "Player1";
            var objectOwner = "Player2";

            var authorizeCommand = new AuthorizeCommand(innerCommand, subject, objectOwner);

            Assert.Throws<InvalidOperationException>(() => authorizeCommand.Execute());
            innerCommand.DidNotReceive().Execute();
        }

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenCommandIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new AuthorizeCommand(null!, "Player1", "Player1"));
        }

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenSubjectIsNull()
        {
            var innerCommand = Substitute.For<ICommand>();
            Assert.Throws<ArgumentNullException>(() => new AuthorizeCommand(innerCommand, null!, "Player1"));
        }

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenObjectOwnerIsNull()
        {
            var innerCommand = Substitute.For<ICommand>();
            Assert.Throws<ArgumentNullException>(() => new AuthorizeCommand(innerCommand, "Player1", null!));
        }
    }
}
