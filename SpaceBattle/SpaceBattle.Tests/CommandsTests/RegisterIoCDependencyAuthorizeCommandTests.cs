using App;
using App.Scopes;
using NSubstitute;
using SpaceBattle.Lib;
using System;
using Xunit;

namespace SpaceBattle.Tests
{
    public class RegisterIoCDependencyAuthorizeCommandTests
    {
        public RegisterIoCDependencyAuthorizeCommandTests()
        {
            new InitCommand().Execute();
            var scope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", scope).Execute();
        }

        [Fact]
        public void Execute_ShouldRegisterAuthorizeCommand_AndItShouldBeResolvable()
        {
            var registerCommand = new RegisterIoCDependencyAuthorizeCommand();
            registerCommand.Execute();

            var innerCommand = Substitute.For<ICommand>();
            var subject = "Player1";
            var objectOwner = "Player1";

            var command = Ioc.Resolve<ICommand>("Commands.Authorize", innerCommand, subject, objectOwner);

            Assert.NotNull(command);
            Assert.IsType<AuthorizeCommand>(command);
        }

        [Fact]
        public void Execute_ShouldRegisterAuthorizeCommand_ThatCanExecuteInnerCommand()
        {
            var registerCommand = new RegisterIoCDependencyAuthorizeCommand();
            registerCommand.Execute();

            var innerCommand = Substitute.For<ICommand>();
            var subject = "Player1";
            var objectOwner = "Player1";

            var command = Ioc.Resolve<ICommand>("Commands.Authorize", innerCommand, subject, objectOwner);
            command.Execute();

            innerCommand.Received(1).Execute();
        }

        [Fact]
        public void Execute_ShouldRegisterAuthorizeCommand_ThatThrowsWhenUnauthorized()
        {
            var registerCommand = new RegisterIoCDependencyAuthorizeCommand();
            registerCommand.Execute();

            var innerCommand = Substitute.For<ICommand>();
            var subject = "Player1";
            var objectOwner = "Player2";

            var command = Ioc.Resolve<ICommand>("Commands.Authorize", innerCommand, subject, objectOwner);

            Assert.Throws<InvalidOperationException>(() => command.Execute());
            innerCommand.DidNotReceive().Execute();
        }
    }
}
