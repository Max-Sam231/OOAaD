using System;
using Xunit;
using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests
{
    public class RegisterIoCDependencyCollisionDetectorTests
    {
        public RegisterIoCDependencyCollisionDetectorTests()
        {
            new InitCommand().Execute();
            var scope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", scope).Execute();
        }

        [Fact]
        public void Execute_RegistersDependency_ResolvesCollisionDetector()
        {
            new RegisterIoCDependencyCollisionDetector().Execute();

            var detector = Ioc.Resolve<ICollisionDetector>("Collision.Detector", 10);

            Assert.NotNull(detector);
            Assert.IsType<QuadrantCollisionDetector>(detector);
        }
    }
}
