using Xunit;
using SpaceBattle.Lib;
using NSubstitute;

namespace SpaceBattle.Lib.Tests
{
    public class RotateCommandTests
    {
        [Fact]
        public void RotateCommand_Execute_ShouldRotate()
        {
            var mockRotation = Substitute.For<IRotatable>();

            mockRotation.Angle.Returns(new Angle(1, 8));
            mockRotation.AngularVelocity.Returns(new Angle(1, 8));

            var command = new RotateCommand(mockRotation);
            command.Execute();

            mockRotation.Received().Angle = new Angle(2);
        }

        [Fact]
        public void RotateCommand_Execute_WhenCannotDetermineAngle_ShouldThrowException()
        {
            var mockRotation = Substitute.For<IRotatable>();

            mockRotation.Angle.Returns(_ => throw new InvalidOperationException());
            mockRotation.AngularVelocity.Returns(new Angle(1, 8));

            var command = new RotateCommand(mockRotation);

            Assert.ThrowsAny<InvalidOperationException>(() => command.Execute());
        }

        [Fact]
        public void RotateCommand_Execute_WhenCannotDetermineAngularVelocity_ShouldThrowException()
        {
            var mockRotation = Substitute.For<IRotatable>();

            mockRotation.Angle.Returns(new Angle(1, 8));
            mockRotation.AngularVelocity.Returns(_ => throw new InvalidOperationException());

            var command = new RotateCommand(mockRotation);

            Assert.ThrowsAny<InvalidOperationException>(() => command.Execute());
        }

        [Fact]
        public void RotateCommand_Execute_WhenCannotChangeAngle_ShouldThrowException()
        {
            var mockRotation = Substitute.For<IRotatable>();

            mockRotation.Angle.Returns(new Angle(1, 8));
            mockRotation.AngularVelocity.Returns(new Angle(1, 8));
            mockRotation.When(x => x.Angle = Arg.Any<Angle>()).Do(_ => throw new InvalidOperationException());

            var command = new RotateCommand(mockRotation);

            Assert.ThrowsAny<InvalidOperationException>(() => command.Execute());
        }

        [Fact]
        public void Constructor_NullRotatable_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new RotateCommand(null!));
        }
    }
}
