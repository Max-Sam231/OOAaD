using Xunit;
using NSubstitute;
using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests
{
    public class RegisterIoCDependencyRotateCommandTest
    {
        public RegisterIoCDependencyRotateCommandTest()
        {
            new InitCommand().Execute();

            var scope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", scope).Execute();
        }

        [Fact]
        public void Execute_ShouldRegisterRotateCommand_AndItShouldBeResolvable()
        {
            var mockRotatable = Substitute.For<IRotatable>();

            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Adapters.IRotatable",
                (Func<object[], object>)(args => mockRotatable)
            ).Execute();

            var registerCommand = new RegisterIoCDependencyRotateCommand();
            registerCommand.Execute();

            var command = Ioc.Resolve<ICommand>("Commands.Rotate", new object[] { });

            Assert.NotNull(command);
            Assert.IsType<RotateCommand>(command);
        }

        [Fact]
        public void ResolvedRotateCommand_ShouldUpdateAngle()
        {
            var mockRotatable = Substitute.For<IRotatable>();
            mockRotatable.Angle.Returns(new Angle(1, 8));
            mockRotatable.AngularVelocity.Returns(new Angle(1, 8));

            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Adapters.IRotatable",
                (Func<object[], object>)(args => mockRotatable)
            ).Execute();

            new RegisterIoCDependencyRotateCommand().Execute();

            var rotateCmd = Ioc.Resolve<ICommand>("Commands.Rotate", new object[] { });

            rotateCmd.Execute();

            mockRotatable.Received().Angle = new Angle(2);
        }
    }
}