using SpaceBattle.Lib;
using Xunit;
using NSubstitute;
using App.Scopes;
using App;

using ICommand = SpaceBattle.Lib.ICommand;

namespace SpaceBattle.Tests
{
    public class RegisterIoCDependencyMacroCommandTest
    {
        public RegisterIoCDependencyMacroCommandTest()
        {
            new InitCommand().Execute();

            var scope = Ioc.Resolve<object>("IoC.Scope.Create");

            Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", scope).Execute();
        }

        [Fact]
        public void Execute_ShouldRegisterMacroCommand_AndItShouldBeResolvable()
        {
            var registerCmd = new RegisterIoCDependencyMacroCommand();
            registerCmd.Execute();

            var mockCmd1 = Substitute.For<ICommand>();
            var mockCmd2 = Substitute.For<ICommand>();
            var commands = new ICommand[] {mockCmd1, mockCmd2};

            var macroCommand = Ioc.Resolve<ICommand>("Command.Macro", new object[] {commands}); 

            Assert.NotNull(macroCommand);
            Assert.IsType<MacroCommand>(macroCommand);

            macroCommand.Execute();
            mockCmd1.Received(1).Execute();
            mockCmd2.Received(1).Execute();
        }
    }
}