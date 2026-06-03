using System;
using Xunit;
using NSubstitute;
using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests
{
    public class CheckCollisionCommandTests
    {
        public CheckCollisionCommandTests()
        {
            new InitCommand().Execute();
            var scope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", scope).Execute();
        }

        [Fact]
        public void Execute_CollisionDetected_CallsEventsCollision()
        {
            var a = Substitute.For<ICollidable>();
            var b = Substitute.For<ICollidable>();
            var detector = Substitute.For<ICollisionDetector>();
            var eventCommand = Substitute.For<ICommand>();

            detector.DetectCollision(a, b).Returns(true);

            Ioc.Resolve<ICommand>("IoC.Register", "Events.Collision", (Func<object[], object>)(args => eventCommand)).Execute();

            var command = new CheckCollisionCommand(a, b, detector);
            command.Execute();

            eventCommand.Received(1).Execute();
        }

        [Fact]
        public void Execute_NoCollision_DoesNotCallEventsCollision()
        {
            var a = Substitute.For<ICollidable>();
            var b = Substitute.For<ICollidable>();
            var detector = Substitute.For<ICollisionDetector>();
            var eventCommand = Substitute.For<ICommand>();

            detector.DetectCollision(a, b).Returns(false);

            Ioc.Resolve<ICommand>("IoC.Register", "Events.Collision", (Func<object[], object>)(args => eventCommand)).Execute();

            var command = new CheckCollisionCommand(a, b, detector);
            command.Execute();

            eventCommand.DidNotReceive().Execute();
        }

        [Fact]
        public void Constructor_NullCollidableA_ThrowsArgumentNullException()
        {
            var b = Substitute.For<ICollidable>();
            var detector = Substitute.For<ICollisionDetector>();

            Assert.Throws<ArgumentNullException>(() => new CheckCollisionCommand(null!, b, detector));
        }

        [Fact]
        public void Constructor_NullCollidableB_ThrowsArgumentNullException()
        {
            var a = Substitute.For<ICollidable>();
            var detector = Substitute.For<ICollisionDetector>();

            Assert.Throws<ArgumentNullException>(() => new CheckCollisionCommand(a, null!, detector));
        }

        [Fact]
        public void Constructor_NullDetector_ThrowsArgumentNullException()
        {
            var a = Substitute.For<ICollidable>();
            var b = Substitute.For<ICollidable>();

            Assert.Throws<ArgumentNullException>(() => new CheckCollisionCommand(a, b, null!));
        }
    }
}
