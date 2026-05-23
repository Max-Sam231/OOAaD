using Xunit;
using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests
{
    public class RegisterIoCDependencyCommandInjectableTests
    {
        public RegisterIoCDependencyCommandInjectableTests()
        {
            new InitCommand().Execute();

            var scope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", scope).Execute();
        }

        [Fact]
        public void RegisterIoCDependencyCommandInjectableTests_ShouldRegisterDependency()
        {

            var registerCmd = new RegisterIoCDependencyCommandInjectable();


            registerCmd.Execute();

            var resolvedAsCommand = Ioc.Resolve<ICommand>("Commands.CommandInjectable");
            Assert.NotNull(resolvedAsCommand);
            Assert.IsType<CommandInjectableCommand>(resolvedAsCommand);

            var resolvedAsInjectable = Ioc.Resolve<ICommandInjectable>("Commands.CommandInjectable");
            Assert.NotNull(resolvedAsInjectable);
            Assert.IsType<CommandInjectableCommand>(resolvedAsInjectable);

            var resolvedAsConcrete = Ioc.Resolve<CommandInjectableCommand>("Commands.CommandInjectable");
            Assert.NotNull(resolvedAsConcrete);
        }
    }
}
