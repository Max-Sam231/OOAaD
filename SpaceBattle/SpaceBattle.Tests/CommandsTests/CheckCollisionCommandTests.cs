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
        public void Execute_CollisionDetected_InvokesCollisionEvent()
        {
            var obj1 = Substitute.For<ICollidable>();
            obj1.Position.Returns(new Vector(5, 5));
            obj1.Velocity.Returns(new Vector(10, 0));

            var obj2 = Substitute.For<ICollidable>();
            obj2.Position.Returns(new Vector(15, 5));
            obj2.Velocity.Returns(new Vector(0, 0));

            var eventCommand = Substitute.For<ICommand>();

            Ioc.Resolve<ICommand>("IoC.Register", "Game.Collision.Check",
                (Func<object[], object>)(args =>
                {
                    int dx = (int)args[0];
                    int dy = (int)args[1];
                    int ddx = (int)args[2];
                    int ddy = (int)args[3];

                    return dx == -10 && dy == 0 && ddx == 10 && ddy == 0;
                })).Execute();

            Ioc.Resolve<ICommand>("IoC.Register", "Game.Collision.Event",
                (Func<object[], object>)(args => eventCommand)).Execute();

            var command = new CheckCollisionCommand(obj1, obj2);

            command.Execute();

            eventCommand.Received(1).Execute();
        }

        [Fact]
        public void Execute_NoCollision_DoesNotInvokeCollisionEvent()
        {
            var obj1 = Substitute.For<ICollidable>();
            obj1.Position.Returns(new Vector(0, 0));
            obj1.Velocity.Returns(new Vector(1, 1));

            var obj2 = Substitute.For<ICollidable>();
            obj2.Position.Returns(new Vector(100, 100));
            obj2.Velocity.Returns(new Vector(-1, -1));

            var eventCommand = Substitute.For<ICommand>();

            Ioc.Resolve<ICommand>("IoC.Register", "Game.Collision.Check",
                (Func<object[], object>)(args => false)).Execute();

            Ioc.Resolve<ICommand>("IoC.Register", "Game.Collision.Event",
                (Func<object[], object>)(args => eventCommand)).Execute();

            var command = new CheckCollisionCommand(obj1, obj2);

            command.Execute();

            eventCommand.DidNotReceive().Execute();
        }

        [Fact]
        public void Constructor_NullObj1_ThrowsArgumentNullException()
        {
            var obj2 = Substitute.For<ICollidable>();
            Assert.Throws<ArgumentNullException>(() => new CheckCollisionCommand(null!, obj2));
        }

        [Fact]
        public void Constructor_NullObj2_ThrowsArgumentNullException()
        {
            var obj1 = Substitute.For<ICollidable>();
            Assert.Throws<ArgumentNullException>(() => new CheckCollisionCommand(obj1, null!));
        }
    }
}
