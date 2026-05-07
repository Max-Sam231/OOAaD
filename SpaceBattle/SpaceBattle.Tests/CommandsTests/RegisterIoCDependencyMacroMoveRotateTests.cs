using System;
using System.Collections.Generic;
using NSubstitute;
using Xunit;
using SpaceBattle.Lib;
using App.Scopes;
using App;

using ICommand = SpaceBattle.Lib.ICommand;

namespace SpaceBattle.Tests
{
    public class RegisterIoCDependencyMacroMoveRotateTests
    {
        public RegisterIoCDependencyMacroMoveRotateTests()
        {
            new InitCommand().Execute();

            var scope = Ioc.Resolve<object>("IoC.Scope.Create");

            Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", scope).Execute();
        }

        [Fact]
        public void Execute_ShouldRegisterMacroMove_AndItShouldBeResolvable()
        {
            var registerCmd = new RegisterIoCDependencyMacroMoveRotate();
            registerCmd.Execute();

            var mockCmd1 = Substitute.For<ICommand>();
            var mockCmd2 = Substitute.For<ICommand>();
            var mockMacroCommand = Substitute.For<ICommand>();

            Ioc.Resolve<App.ICommand>("IoC.Register", "Specs.Move", 
                (object[] a) => new List<string> { "Command.Move1", "Command.Move2" }).Execute();
            
            Ioc.Resolve<App.ICommand>("IoC.Register", "Command.Move1", 
                (object[] a) => mockCmd1).Execute();
                
            Ioc.Resolve<App.ICommand>("IoC.Register", "Command.Move2", 
                (object[] a) => mockCmd2).Execute();
                
            Ioc.Resolve<App.ICommand>("IoC.Register", "Commands.Macro", 
                (object[] a) => {
                    var commands = (IEnumerable<ICommand>)a[0];
                    var macro = new MacroCommand(commands.ToArray());
                    return macro;
                }).Execute();

            var result = Ioc.Resolve<ICommand>("Macro.Move", new object[] { new object() });

            Assert.NotNull(result);
            Assert.IsType<MacroCommand>(result);
        }

        [Fact]
        public void Execute_ShouldRegisterMacroRotate_AndItShouldBeResolvable()
        {
            var registerCmd = new RegisterIoCDependencyMacroMoveRotate();
            registerCmd.Execute();

            var mockCmd1 = Substitute.For<ICommand>();
            var mockCmd2 = Substitute.For<ICommand>();
            var mockMacroCommand = Substitute.For<ICommand>();

            Ioc.Resolve<App.ICommand>("IoC.Register", "Specs.Rotate", 
                (object[] a) => new List<string> { "Command.Rotate1", "Command.Rotate2" }).Execute();
            
            Ioc.Resolve<App.ICommand>("IoC.Register", "Command.Rotate1", 
                (object[] a) => mockCmd1).Execute();
                
            Ioc.Resolve<App.ICommand>("IoC.Register", "Command.Rotate2", 
                (object[] a) => mockCmd2).Execute();
                
            Ioc.Resolve<App.ICommand>("IoC.Register", "Commands.Macro", 
                (object[] a) => {
                    var commands = (IEnumerable<ICommand>)a[0];
                    var macro = new MacroCommand(commands.ToArray());
                    return macro;
                }).Execute();

            var result = Ioc.Resolve<ICommand>("Macro.Rotate", new object[] { new object() });

            Assert.NotNull(result);
            Assert.IsType<MacroCommand>(result);
        }

        [Fact]
        public void Execute_ShouldRegisterBothDependencies_WithoutThrowingException()
        {
            var registerCmd = new RegisterIoCDependencyMacroMoveRotate();

            var exception = Record.Exception(() => registerCmd.Execute());
            
            Assert.Null(exception);
        }
    }
}
