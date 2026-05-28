using App;
using App.Scopes;
using NSubstitute;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Tests
{
    public class MoveTorpedoCommandTests
    {
        [Fact]
        public void Execute_ShouldResolveAndRunMoveCommandForTorpedo()
        {
            new InitCommand().Execute();
            var scope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", scope).Execute();

            var repository = Substitute.For<IReadOnlyGameObjectRepository>();
            var torpedo = new Dictionary<string, object>();
            var moveCommand = Substitute.For<ICommand>();

            repository.Get(7).Returns(torpedo);

            Ioc.Resolve<ICommand>("IoC.Register", "Commands.Move", (Func<object[], object>)(args =>
            {
                Assert.Same(torpedo, args[0]);
                return moveCommand;
            })).Execute();

            var command = new MoveTorpedoCommand(7, repository);
            command.Execute();

            moveCommand.Received(1).Execute();
        }

        [Fact]
        public void Execute_ShouldThrow_WhenTorpedoNotFoundInRepository()
        {
            var repository = Substitute.For<IReadOnlyGameObjectRepository>();
            repository.Get(404).Returns(_ => throw new KeyNotFoundException());

            var command = new MoveTorpedoCommand(404, repository);

            Assert.Throws<KeyNotFoundException>(() => command.Execute());
        }

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenRepositoryIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new MoveTorpedoCommand(1, null!));
        }
    }
}
