using System;
using Xunit;
using NSubstitute;
using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests
{
    public class RegisterIoCDependencyCheckCollisionCommandTests
    {
        public RegisterIoCDependencyCheckCollisionCommandTests()
        {
            new InitCommand().Execute();
            var scope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", scope).Execute();
        }

        [Fact]
        public void Execute_RegistersDependency_ResolvesCheckCollisionCommand()
        {
            new RegisterIoCDependencyCheckCollisionCommand().Execute();

            var a = Substitute.For<ICollidable>();
            var b = Substitute.For<ICollidable>();
            var detector = Substitute.For<ICollisionDetector>();

            var command = Ioc.Resolve<ICommand>("Commands.CheckCollision", a, b, detector);

            Assert.NotNull(command);
            Assert.IsType<CheckCollisionCommand>(command);
        }
    }
}
