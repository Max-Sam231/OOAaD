using App;
using App.Scopes;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Test
{
    public class RegisterIoCDependencyCommandWithAdapterTests
    {
        public RegisterIoCDependencyCommandWithAdapterTests()
        {
            new InitCommand().Execute();

            var scope = Ioc.Resolve<object>("IoC.Scope.Create");

            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", scope).Execute();
        }

        [Fact]
        public void Constructor_WithNullDependencyName_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => new RegisterIoCDependencyCommandWithAdapter(null!, typeof(TestMoveCommand)));
        }

        [Fact]
        public void Constructor_WithNullCommandType_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => new RegisterIoCDependencyCommandWithAdapter("Commands.TestMove", null!));
        }

        [Theory]
        [InlineData(typeof(TestNotACommand))]
        [InlineData(typeof(TestTwoConstructorsCommand))]
        [InlineData(typeof(TestNoParameterCommand))]
        [InlineData(typeof(TestNonInterfaceParameterCommand))]
        public void Execute_WithUnsupportedCommandType_ThrowsArgumentException(Type commandType)
        {
            Assert.Throws<ArgumentException>(
                () => new RegisterIoCDependencyCommandWithAdapter("Commands.Unsupported", commandType).Execute());
        }

        [Fact]
        public void Execute_RegistersCommandBuiltOnGeneratedAdapter()
        {
            new RegisterIoCDependencyAdapterFactory().Execute();
            new RegisterIoCDependencyAdapter(typeof(ITestMovingObject)).Execute();
            new RegisterIoCDependencyCommandWithAdapter("Commands.TestMove", typeof(TestMoveCommand)).Execute();
            var gameItem = new Dictionary<string, object> { ["Position"] = new Vector(8, 9) };

            var command = Ioc.Resolve<ICommand>("Commands.TestMove", gameItem);

            var testMove = Assert.IsType<TestMoveCommand>(command);
            Assert.Equal(new Vector(8, 9), testMove.Obj.Position);
        }
    }
}
