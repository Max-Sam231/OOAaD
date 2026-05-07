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
    public class CreateMacroCommandStrategyTests
    {
        public CreateMacroCommandStrategyTests()
        {
            new InitCommand().Execute();

            var scope = Ioc.Resolve<object>("IoC.Scope.Create");

            Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", scope).Execute();
        }

        [Fact]
        public void Resolve_SuccessfulCreation_ReturnsMacroCommand()
        {
            string specName = "TestMoveWithFuel";
            var strategy = new CreateMacroCommandStrategy(specName);

            var mockTarget = Substitute.For<IDictionary<string, object>>();
            object[] args = { mockTarget };

            var mockCmd1 = Substitute.For<ICommand>();
            var mockCmd2 = Substitute.For<ICommand>();
            var mockMacroCommand = Substitute.For<ICommand>();

            Ioc.Resolve<App.ICommand>("IoC.Register", $"Specs.{specName}",
                (object[] a) => new List<string> { "Command.Test1", "Command.Test2" }).Execute();

            Ioc.Resolve<App.ICommand>("IoC.Register", "Command.Test1",
                (object[] a) => mockCmd1).Execute();

            Ioc.Resolve<App.ICommand>("IoC.Register", "Command.Test2",
                (object[] a) => mockCmd2).Execute();

            Ioc.Resolve<App.ICommand>("IoC.Register", "Commands.Macro",
                (object[] a) =>
                {
                    var commands = (IEnumerable<ICommand>)a[0];
                    var macro = new MacroCommand(commands.ToArray());
                    return macro;
                }).Execute();

            var result = strategy.Resolve(args);

            Assert.NotNull(result);
            Assert.IsType<MacroCommand>(result);

            result.Execute();
            mockCmd1.Received(1).Execute();
            mockCmd2.Received(1).Execute();
        }

        [Fact]
        public void Resolve_DependencyNotFound_ThrowsException()
        {
            string specName = "UnknownSpec";
            var strategy = new CreateMacroCommandStrategy(specName);
            object[] args = { new object() };

            Assert.Throws<Exception>(() => strategy.Resolve(args));
        }
    }
}
