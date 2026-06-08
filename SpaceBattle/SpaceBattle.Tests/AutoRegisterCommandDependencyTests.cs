using System;
using System.Collections.Generic;
using Xunit;
using NSubstitute;
using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests
{
    [Adapter(typeof(ITestMovingObject), "Velocity", "CustomStrategy.GetVelocity")]
    public interface ITestMovingObject
    {
        Vector Location { get; set; }
        Vector Velocity { get; }
    }

    public class TestMoveCommand : ICommand
    {
        public ITestMovingObject Obj { get; }
        public TestMoveCommand(ITestMovingObject obj) => Obj = obj;
        public void Execute() { }
    }

    public class AutoRegisterCommandDependencyTests
    {
        public AutoRegisterCommandDependencyTests()
        {
            new InitCommand().Execute();

            var scope = Ioc.Resolve<object>("IoC.Scope.Create");

            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", scope).Execute();
        }

        [Fact]
        public void AutoRegisterCommandDependency_ShouldGenerateAdapterAndResolveCommand()
        {
            var regCommand = new AutoRegisterCommandDependency("Commands.TestMove", typeof(TestMoveCommand));
            regCommand.Execute();

            var mockLocation = new Vector(10, 20);
            var mockVelocity = new Vector(2, 3);

            Ioc.Resolve<ICommand>("IoC.Register", "ITestMovingObject:Location.Get", new Func<object[], object>(args => mockLocation)).Execute();
            
            Ioc.Resolve<ICommand>("IoC.Register", "CustomStrategy.GetVelocity", new Func<object[], object>(args => mockVelocity)).Execute();

            var rawObj = new Dictionary<string, object>();

            var resolvedCommand = Ioc.Resolve<ICommand>("Commands.TestMove", rawObj) as TestMoveCommand;

            Assert.NotNull(resolvedCommand);
            Assert.NotNull(resolvedCommand.Obj);
            
            Assert.Equal(mockLocation, resolvedCommand.Obj.Location);
            Assert.Equal(mockVelocity, resolvedCommand.Obj.Velocity);
        }
    }
}
