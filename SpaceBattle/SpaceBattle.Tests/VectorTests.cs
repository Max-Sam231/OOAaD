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

        [Fact]
        public void Constructor_NullArray_CreatesEmptyVector()
        {
            int[]? nullArray = null;
            var v = new Vector(nullArray!);
            var expected = new Vector();

            Assert.Equal(expected, v);
        }

        [Fact]
        public void Equals_NullObject_ReturnsFalse()
        {
            var v1 = new Vector(1, 2, 3);

            Assert.False(v1.Equals(null!));
        }

        [Fact]
        public void Equals_DifferentTypeObject_ReturnsFalse()
        {
            var v1 = new Vector(1, 2, 3);

            Assert.False(v1.Equals("Строка"));
        }

        [Fact]
        public void OperatorEquals_BothNull_ReturnsTrue()
        {
            Vector? v1 = null!;
            Vector? v2 = null!;

            Assert.True(v1 == v2);
        }

        [Fact]
        public void OperatorEquals_FirstNull_ReturnsFalse()
        {
            Vector? v1 = null!;
            var v2 = new Vector(1, 2, 3);

            Assert.False(v1 == v2);
        }

        [Fact]
        public void OperatorEquals_SecondNull_ReturnsFalse()
        {
            var v1 = new Vector(1, 2, 3);
            Vector? v2 = null!;

            Assert.False(v1 == v2);
        }

        [Fact]
        public void OperatorNotEquals_OneNull_ReturnsTrue()
        {
            var v1 = new Vector(1, 2, 3);
            Vector? v2 = null!;

            Assert.True(v1 != v2);
        }

        [Fact]
        public void Equals_BothNull_ReturnsFalse()
        {
            Vector? v1 = null!;
            Vector? v2 = null!;

            Assert.True(Vector.Equals(v1, v2));
        }

        [Fact]
        public void Equals_FirstNull_ReturnsFalse()
        {
            Vector? v1 = null!;
            var v2 = new Vector(1, 2, 3);

            Assert.False(Vector.Equals(v1, v2));
        }

        [Fact]
        public void Equals_SecondNull_ReturnsFalse()
        {
            var v1 = new Vector(1, 2, 3);
            Vector? v2 = null!;

            Assert.False(Vector.Equals(v1, v2));
        }
    }
}
