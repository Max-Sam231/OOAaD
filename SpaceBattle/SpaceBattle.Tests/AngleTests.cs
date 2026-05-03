using System;
using Xunit;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests
{
    public class AngleTests
    {
        [Fact]
        public void Add_TwoAngles_ReturnsNormalizedSum()
        {
            var angle1 = new Angle(5, 8);
            var angle2 = new Angle(7, 8);
            var expected = new Angle(4, 8);

            var result = angle1 + angle2;

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Equals_EquivalentAngles_ReturnsTrue()
        {
            var angle1 = new Angle(15, 8);
            var angle2 = new Angle(23, 8);

            Assert.True(angle1.Equals(angle2));
        }

        [Fact]
        public void EqualityOperator_EquivalentAngles_ReturnsTrue()
        {
            var angle1 = new Angle(15, 8);
            var angle2 = new Angle(23, 8);

            Assert.True(angle1 == angle2);
        }

        [Fact]
        public void Equals_DifferentAngles_ReturnsFalse()
        {
            var angle1 = new Angle(1, 8);
            var angle2 = new Angle(2, 8);

            Assert.False(angle1.Equals(angle2));
        }

        [Fact]
        public void InequalityOperator_DifferentAngles_ReturnsTrue()
        {
            var angle1 = new Angle(1, 8);
            var angle2 = new Angle(2, 8);

            Assert.True(angle1 != angle2);
        }

        [Fact]
        public void GetHashCode_EquivalentAngles_ReturnsSameHashCode()
        {
            var angle1 = new Angle(15, 8);
            var angle2 = new Angle(23, 8);

            Assert.Equal(angle1.GetHashCode(), angle2.GetHashCode());
        }

        [Fact]
        public void ImplicitConversionToDouble_CalculatesTrigonometryCorrectly()
        {
            var angle = new Angle(8, 8);

            double cosValue = Math.Cos(angle);
            double sinValue = Math.Sin(angle);

            Assert.Equal(1.0, cosValue, 5);
            Assert.Equal(0.0, sinValue, 5);
        }
    }
}
