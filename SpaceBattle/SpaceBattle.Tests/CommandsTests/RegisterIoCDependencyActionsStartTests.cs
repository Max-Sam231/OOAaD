using App;
using App.Scopes;
using NSubstitute;
using SpaceBattle.Lib;
using System;
using System.Collections.Generic;
using Xunit;

namespace SpaceBattle.Tests
{
    public class RegisterIoCDependencyActionsStartTests
    {
        public RegisterIoCDependencyActionsStartTests()
        {
            new InitCommand().Execute();
            var scope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", scope).Execute();
        }

        [Fact]
        public void Execute_ShouldRegisterActionsStartDependency_AndResolveCorrectly()
        {
            // Arrange
            var mockInjectable = Substitute.For<ICommandInjectable>();
            Ioc.Resolve<ICommand>("IoC.Register", "Commands.CommandInjectable", new Func<object[], object>((args) => mockInjectable)).Execute();

            var mockSendCommand = Substitute.For<ICommand>();
            Ioc.Resolve<ICommand>("IoC.Register", "Commands.Send", new Func<object[], object>((args) => mockSendCommand)).Execute();

            var registerCommand = new RegisterIoCDependencyActionsStart();
            registerCommand.Execute();

            var mockReceiver = Substitute.For<ICommandReceiver>();
            var mockTarget = Substitute.For<ICommand>();

            var order = new Dictionary<string, object>
            {
                { "Receiver", mockReceiver },
                { "Target", mockTarget }
            };

            // Act
            var resultCommand = Ioc.Resolve<ICommand>("Actions.Start", order);

            Assert.NotNull(resultCommand);
            Assert.Same(mockSendCommand, resultCommand);

            
            mockInjectable.Received(1).Inject(mockTarget);
        }
    }
}
