using Xunit;
using NSubstitute;
using App;
using App.Scopes;
using SpaceBattle.Lib;
using System;

namespace SpaceBattle.Lib.Test
{
    public class RegisterIoCDependencyMoveCommandTest
    {
        public RegisterIoCDependencyMoveCommandTest()
        {
            new InitCommand().Execute();

            var scope = Ioc.Resolve<object>("IoC.Scope.Create");
            ((dynamic)Ioc.Resolve<object>("IoC.Scope.Current.Set", scope)).Execute();
        }

        [Fact]
        public void Execute_ShouldRegisterMoveCommand_AndItShouldBeResolvable()
        {
            var mockMovable = Substitute.For<IMovable>();

            ((dynamic)Ioc.Resolve<object>(
                "IoC.Register",
                "Adapters.IMovable",
                (Func<object[], object>)(args => mockMovable)
            )).Execute();

            var registerCommand = new RegisterIoCDependencyMoveCommand();

            registerCommand.Execute();

            var command = Ioc.Resolve<SpaceBattle.Lib.ICommand>("Commands.Move", new object());

            Assert.NotNull(command);
            Assert.IsType<MoveCommand>(command);
        }

        [Fact]
        public void ResolvedMoveCommand_ShouldUpdatePosition()
        {
            var mockMovable = Substitute.For<IMovable>();
            mockMovable.Position.Returns(new Vector(10, 20));
            mockMovable.Velocity.Returns(new Vector(5, 5));

            ((dynamic)Ioc.Resolve<object>(
                "IoC.Register",
                "Adapters.IMovable",
                (Func<object[], object>)(args => mockMovable)
            )).Execute();

            new RegisterIoCDependencyMoveCommand().Execute();

            var moveCmd = Ioc.Resolve<SpaceBattle.Lib.ICommand>("Commands.Move", new object());

            moveCmd.Execute();

            mockMovable.Received().Position = new Vector(15, 25);
        }
    }
}
