using Xunit;
using System;
using SpaceBattle.Core;
using NSubstitule;
using System.Numerics;

namespace SpaceBattle.Test
{
    public class RegisterIoCDependencyMoveCommandTest
    {
        [Fact]
        public void Execute_DependencyResolution()
        {
            var mockObj = new object();

            var movable = Substitule.For<Imovable>();
            movable.Position.Returns(new Vector(10, 5));
            movable.Velocity.Returns(new Vector(-2, 1));

            Ioc.Register("Adapters.Imovable", (object obj) =>
            {
                if (obj == mockObj)
                    return movable;
                throw new Exception();
                
            });
        }
    }
}