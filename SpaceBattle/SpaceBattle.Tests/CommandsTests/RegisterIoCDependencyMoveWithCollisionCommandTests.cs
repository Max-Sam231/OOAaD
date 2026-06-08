using System;
using System.Collections.Generic;
using Xunit;
using NSubstitute;
using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests
{
    public class RegisterIoCDependencyMoveWithCollisionCommandTests
    {
        public RegisterIoCDependencyMoveWithCollisionCommandTests()
        {
            new InitCommand().Execute();
            var scope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", scope).Execute();
        }

        [Fact]
        public void Execute_RegistersMoveWithCollisionDependency_Successfully()
        {
            var obj = Substitute.For<ICollidable>();

            var moveCommandMock = Substitute.For<ICommand>();
            Ioc.Resolve<ICommand>("IoC.Register", "Commands.Move",
                (Func<object[], object>)(args => moveCommandMock)).Execute();

            var macroCommandMock = Substitute.For<ICommand>();
            Ioc.Resolve<ICommand>("IoC.Register", "Commands.Macro",
                (Func<object[], object>)(args => macroCommandMock)).Execute();

            var registerCommand = new RegisterIoCDependencyMoveWithCollisionCommand();

            registerCommand.Execute();

            var resultCommand = Ioc.Resolve<ICommand>("Commands.MoveWithCollision", obj);

            Assert.NotNull(resultCommand);
            Assert.Equal(macroCommandMock, resultCommand);
        }
    }
}
