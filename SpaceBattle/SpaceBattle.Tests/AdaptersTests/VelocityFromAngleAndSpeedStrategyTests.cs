using App;
using App.Scopes;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Test
{
    public class VelocityFromAngleAndSpeedStrategyTests
    {
        public VelocityFromAngleAndSpeedStrategyTests()
        {
            new InitCommand().Execute();

            var scope = Ioc.Resolve<object>("IoC.Scope.Create");

            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", scope).Execute();
        }

        [Theory]
        [InlineData(0, 8, 3, 3, 0)]
        [InlineData(2, 8, 3, 0, 3)]
        [InlineData(4, 8, 2, -2, 0)]
        [InlineData(6, 8, 5, 0, -5)]
        [InlineData(1, 8, 2, 1, 1)]
        [InlineData(1, 8, 3, 2, 2)]
        [InlineData(1, 8, 5, 4, 4)]
        [InlineData(5, 8, 5, -4, -4)]
        public void Resolve_ReturnsExpectedVelocityVector(
            int numerator,
            int denominator,
            int speed,
            int expectedX,
            int expectedY)
        {
            var gameItem = new Dictionary<string, object>
            {
                ["Angle"] = new Angle(numerator, denominator),
                ["Speed"] = speed,
            };

            var result = VelocityFromAngleAndSpeedStrategy.Resolve(new object[] { gameItem });

            Assert.Equal(new Vector(expectedX, expectedY), result);
        }

        [Fact]
        public void Resolve_MissingSpeedOrAngle_ThrowsKeyNotFoundException()
        {
            var missingSpeed = new Dictionary<string, object>
            {
                ["Angle"] = new Angle(0, 8),
            };
            var missingAngle = new Dictionary<string, object>
            {
                ["Speed"] = 3,
            };

            Assert.Throws<KeyNotFoundException>(
                () => VelocityFromAngleAndSpeedStrategy.Resolve(new object[] { missingSpeed }));
            Assert.Throws<KeyNotFoundException>(
                () => VelocityFromAngleAndSpeedStrategy.Resolve(new object[] { missingAngle }));
        }

        [Fact]
        public void AdapterAndMoveAndRotate_RecomputesVelocityOnTheFlyWithoutAccumulatingError()
        {
            new RegisterIoCDependencyAdapterFactory().Execute();
            new RegisterIoCDependencyAdapter(typeof(IMovingObject)).Execute();
            new RegisterIoCDependencyAdapter(typeof(IRotatingObject)).Execute();
            new RegisterIoCDependencyMoveCommand().Execute();
            new RegisterIoCDependencyRotateCommand().Execute();

            var gameItem = new Dictionary<string, object>
            {
                ["Position"] = new Vector(1, 2),
                ["Angle"] = new Angle(0, 8),
                ["AngularVelocity"] = new Angle(2, 8),
                ["Speed"] = 3,
            };

            Ioc.Resolve<ICommand>("Commands.Move", gameItem).Execute();
            Assert.Equal(new Vector(4, 2), gameItem["Position"]);

            Ioc.Resolve<ICommand>("Commands.Rotate", gameItem).Execute();
            Assert.Equal(new Angle(2, 8), gameItem["Angle"]);

            Ioc.Resolve<ICommand>("Commands.Move", gameItem).Execute();
            Assert.Equal(new Vector(4, 5), gameItem["Position"]);

            Ioc.Resolve<ICommand>("Commands.Rotate", gameItem).Execute();
            Ioc.Resolve<ICommand>("Commands.Move", gameItem).Execute();
            Assert.Equal(new Vector(1, 5), gameItem["Position"]);
        }
    }
}
