using App;
using App.Scopes;
using NSubstitute;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Tests
{
    public class RegisterIoCDependencyShootCommandTests
    {
        public RegisterIoCDependencyShootCommandTests()
        {
            new InitCommand().Execute();

            var scope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", scope).Execute();
        }

        [Fact]
        public void Execute_ShouldRegisterShootCommand_AndItShouldBeResolvable()
        {
            var shootable = Substitute.For<IShootable>();

            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Adapters.IShootable",
                (Func<object[], object>)(args => shootable)
            ).Execute();

            var registerCommand = new RegisterIoCDependencyShootCommand();
            registerCommand.Execute();

            var command = Ioc.Resolve<ICommand>("Commands.Shoot", new object[] { });

            Assert.NotNull(command);
            Assert.IsType<ShootCommand>(command);
        }

        [Fact]
        public void ResolvedShootCommand_ShouldSendProjectileCommandToReceiver()
        {
            var projectileCommand = Substitute.For<ICommand>();
            var receiver = Substitute.For<ICommandReceiver>();
            var shootable = Substitute.For<IShootable>();

            shootable.ProjectileCommand.Returns(projectileCommand);
            shootable.Receiver.Returns(receiver);

            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Adapters.IShootable",
                (Func<object[], object>)(args => shootable)
            ).Execute();

            new RegisterIoCDependencyShootCommand().Execute();

            var shootCommand = Ioc.Resolve<ICommand>("Commands.Shoot", new object[] { });
            shootCommand.Execute();

            receiver.Received(1).Receive(projectileCommand);
        }
    }
}
