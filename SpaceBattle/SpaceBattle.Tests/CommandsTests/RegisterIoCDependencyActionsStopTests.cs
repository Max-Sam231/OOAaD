using App;
using App.Scopes;
using NSubstitute;
using SpaceBattle.Lib;
using System;
using System.Collections.Generic;
using Xunit;

namespace SpaceBattle.Tests
{
    public class RegisterIoCDependencyActionsStopTests
    {
        public RegisterIoCDependencyActionsStopTests()
        {
            new InitCommand().Execute();
            var scope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", scope).Execute();
        }

        [Fact]
        public void Execute_ShouldRegisterActionsStopDependency_AndStopInConstantTime()
        {
            var mockEmptyCommand = Substitute.For<ICommand>();
            Ioc.Resolve<ICommand>("IoC.Register", "Commands.Empty", new Func<object[], object>((args) => mockEmptyCommand)).Execute();

            var registerCommand = new RegisterIoCDependencyActionsStop();
            registerCommand.Execute();

            var mockInjectable = Substitute.For<ICommandInjectable>();
            var order = new Dictionary<string, object>
            {
                { "Injectable", mockInjectable }
            };

            var stopCommand = Ioc.Resolve<ICommand>("Actions.Stop", order);
            
            stopCommand.Execute();

            Assert.NotNull(stopCommand);
            mockInjectable.Received(1).Inject(mockEmptyCommand);
        }
    }
}
