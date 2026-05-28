using App;
using App.Scopes;
using NSubstitute;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Tests
{
    public class RegisterIoCDependencyMoveTorpedoCommandTests
    {
        public RegisterIoCDependencyMoveTorpedoCommandTests()
        {
            new InitCommand().Execute();
            var scope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", scope).Execute();
        }

        [Fact]
        public void Execute_ShouldRegisterMoveTorpedoCommand_AndItShouldBeResolvable()
        {
            var repository = Substitute.For<IReadOnlyGameObjectRepository>();

            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Game.Repository.ReadOnly",
                (Func<object[], object>)(args => repository)
            ).Execute();

            var registerCommand = new RegisterIoCDependencyMoveTorpedoCommand();
            registerCommand.Execute();

            var command = Ioc.Resolve<ICommand>("Commands.Torpedo.Move", 9);

            Assert.NotNull(command);
            Assert.IsType<MoveTorpedoCommand>(command);
        }
    }
}
