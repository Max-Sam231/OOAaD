using System;
using Xunit;
using NSubstitute;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests
{
    public class QuadrantCollisionDetectorTests
    {
        [Fact]
        public void DetectCollision_ObjectsInSameQuadrant_ReturnsTrue()
        {
            var detector = new QuadrantCollisionDetector(10);

            var a = Substitute.For<ICollidable>();
            a.Position.Returns(new Vector(5, 5));

            var b = Substitute.For<ICollidable>();
            b.Position.Returns(new Vector(7, 7));

            Assert.True(detector.DetectCollision(a, b));
        }

        [Fact]
        public void DetectCollision_ObjectsInDifferentQuadrants_ReturnsFalse()
        {
            var detector = new QuadrantCollisionDetector(10);

            var a = Substitute.For<ICollidable>();
            a.Position.Returns(new Vector(5, 5));

            var b = Substitute.For<ICollidable>();
            b.Position.Returns(new Vector(15, 15));

            Assert.False(detector.DetectCollision(a, b));
        }

        [Fact]
        public void DetectCollision_ObjectsOnQuadrantBoundary_ReturnsFalse()
        {
            var detector = new QuadrantCollisionDetector(10);

            var a = Substitute.For<ICollidable>();
            a.Position.Returns(new Vector(10, 10));

            var b = Substitute.For<ICollidable>();
            b.Position.Returns(new Vector(9, 9));

            Assert.False(detector.DetectCollision(a, b));
        }

        [Fact]
        public void DetectCollision_NegativeCoordinatesSameQuadrant_ReturnsTrue()
        {
            var detector = new QuadrantCollisionDetector(10);

            var a = Substitute.For<ICollidable>();
            a.Position.Returns(new Vector(-5, -5));

            var b = Substitute.For<ICollidable>();
            b.Position.Returns(new Vector(-3, -3));

            Assert.True(detector.DetectCollision(a, b));
        }

        [Fact]
        public void DetectCollision_NegativeAndPositiveCoordinates_ReturnsFalse()
        {
            var detector = new QuadrantCollisionDetector(10);

            var a = Substitute.For<ICollidable>();
            a.Position.Returns(new Vector(-5, -5));

            var b = Substitute.For<ICollidable>();
            b.Position.Returns(new Vector(5, 5));

            Assert.False(detector.DetectCollision(a, b));
        }

        [Fact]
        public void DetectCollision_NullPositionA_ThrowsInvalidOperationException()
        {
            var detector = new QuadrantCollisionDetector(10);

            var a = Substitute.For<ICollidable>();
            a.Position.Returns((Vector)null!);

            var b = Substitute.For<ICollidable>();
            b.Position.Returns(new Vector(1, 1));

            Assert.Throws<InvalidOperationException>(() => detector.DetectCollision(a, b));
        }

        [Fact]
        public void DetectCollision_NullCollidableA_ThrowsArgumentNullException()
        {
            var detector = new QuadrantCollisionDetector(10);

            var b = Substitute.For<ICollidable>();
            b.Position.Returns(new Vector(1, 1));

            Assert.Throws<ArgumentNullException>(() => detector.DetectCollision(null!, b));
        }

        [Fact]
        public void DetectCollision_NullCollidableB_ThrowsArgumentNullException()
        {
            var detector = new QuadrantCollisionDetector(10);

            var a = Substitute.For<ICollidable>();
            a.Position.Returns(new Vector(1, 1));

            Assert.Throws<ArgumentNullException>(() => detector.DetectCollision(a, null!));
        }

        [Fact]
        public void Constructor_ZeroQuadrantSize_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new QuadrantCollisionDetector(0));
        }

        [Fact]
        public void Constructor_NegativeQuadrantSize_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new QuadrantCollisionDetector(-5));
        }

        [Fact]
        public void DetectCollision_SamePosition_ReturnsTrue()
        {
            var detector = new QuadrantCollisionDetector(10);

            var a = Substitute.For<ICollidable>();
            a.Position.Returns(new Vector(5, 5));

            var b = Substitute.For<ICollidable>();
            b.Position.Returns(new Vector(5, 5));

            Assert.True(detector.DetectCollision(a, b));
        }

        [Fact]
        public void DetectCollision_NullPositionB_ThrowsInvalidOperationException()
        {
            var detector = new QuadrantCollisionDetector(10);

            var a = Substitute.For<ICollidable>();
            a.Position.Returns(new Vector(1, 1));

            var b = Substitute.For<ICollidable>();
            b.Position.Returns((Vector)null!);

            Assert.Throws<InvalidOperationException>(() => detector.DetectCollision(a, b));
        }
    }
}
