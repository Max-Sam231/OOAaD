using System;
using Xunit;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests
{
    public class VectorTests
    {
        [Fact]
        public void Add_ValidVectors_ReturnsCorrectResult()
        {
            var v1 = new Vector(1, -1, 2);
            var v2 = new Vector(-1, 1, -2);
            var expected = new Vector(0, 0, 0);

            var result = v1 + v2;

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Add_FirstVectorLonger_ThrowsArgumentException()
        {
            var v1 = new Vector(1, 2, 3);
            var v2 = new Vector(1, 2);

            Assert.Throws<ArgumentException>(() => v1 + v2);
        }

        [Fact]
        public void Add_SecondVectorLonger_ThrowsArgumentException()
        {
            var v1 = new Vector(1, 2);
            var v2 = new Vector(1, 2, 3);

            Assert.Throws<ArgumentException>(() => v1 + v2);
        }

        [Fact]
        public void Equals_SameCoordinates_ReturnsTrue()
        {
            var v1 = new Vector(1, 2, 3);
            var v2 = new Vector(1, 2, 3);

            Assert.True(v1.Equals(v2));
        }

        [Fact]
        public void OperatorEquals_SameCoordinates_ReturnsTrue()
        {
            var v1 = new Vector(1, 2, 3);
            var v2 = new Vector(1, 2, 3);

            Assert.True(v1 == v2);
        }

        [Fact]
        public void Equals_DifferentCoordinates_ReturnsFalse()
        {
            var v1 = new Vector(1, 2, 3);
            var v2 = new Vector(1, 2, 4);

            Assert.False(v1.Equals(v2));
        }

        [Fact]
        public void OperatorNotEquals_DifferentCoordinates_ReturnsTrue()
        {
            var v1 = new Vector(1, 2, 3);
            var v2 = new Vector(1, 2, 4);

            Assert.True(v1 != v2);
        }

        [Fact]
        public void GetHashCode_ReturnsConsistentValue()
        {
            var v1 = new Vector(1, 2, 3);

            var hashCode = v1.GetHashCode();

            Assert.NotEqual(0, hashCode);
        }
    }
}
