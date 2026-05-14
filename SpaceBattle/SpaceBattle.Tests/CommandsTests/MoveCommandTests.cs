using Xunit;
using SpaceBattle.Lib;
using NSubstitute;

namespace SpaceBattle.Lib.Tests
{
    public class MoveCommandTests
    {
        [Fact]
        public void Execute_UpdatesPositionCorrecty()
        {
            var movable = Substitute.For<IMovable>();

            movable.Position.Returns(new Vector(12, 5));
            movable.Velocity.Returns(new Vector(-4, 1));
            var expected = new Vector(8, 6);

            var moveCommand = new MoveCommand(movable);

            moveCommand.Execute();

            movable.Received().Position = expected;

        }

        [Fact]
        public void Execute_ShouldThrowException_WhenPositionIsUnknown()
        {
            var movable = Substitute.For<IMovable>();

            movable.Position.Returns((Vector)null!);
            movable.Velocity.Returns(new Vector(1, 1));

            var moveCommand = new MoveCommand(movable);

            Assert.Throws<InvalidOperationException>(() => moveCommand.Execute());
        }

        [Fact]
        public void Execute_ShouldThrowException_WhenVelocityIsUnknown()
        {
            var movable = Substitute.For<IMovable>();

            movable.Position.Returns(new Vector(10, 5));
            movable.Velocity.Returns((Vector)null!);

            var moveCommand = new MoveCommand(movable);

            Assert.Throws<InvalidOperationException>(() => moveCommand.Execute());
        }

        [Fact]
        public void Execute_ShouldThrowException_WhenPositionCannotBeChanged()
        {
            var movable = Substitute.For<IMovable>();

            movable.Position.Returns(new Vector(10, 5));
            movable.Velocity.Returns(new Vector(2, -2));

            movable.When(x => x.Position = Arg.Any<Vector>()).Do(x => { throw new InvalidOperationException(); });

            var moveCommand = new MoveCommand(movable);

            Assert.Throws<InvalidOperationException>(() => moveCommand.Execute());
        }

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenMovableIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new MoveCommand(null!));
        }

        [Fact]
        public void Execute_ShouldWrapGenericException_IntoInvalidOperationException()
        {
            var movable = Substitute.For<IMovable>();
            movable.Position.Returns(new Vector(0, 0));
            movable.Velocity.Returns(new Vector(1, 1));
            movable.When(x => x.Position = Arg.Any<Vector>()).Do(x => throw new Exception());

            var moveCommand = new MoveCommand(movable);

            var ex = Assert.Throws<InvalidOperationException>(() => moveCommand.Execute());
            Assert.IsType<Exception>(ex.InnerException);
        }
    }
}
