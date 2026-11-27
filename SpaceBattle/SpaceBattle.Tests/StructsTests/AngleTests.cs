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
        [Fact]
        public void Constructor_ZeroOrNegativeDenominator_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new Angle(5, 0));
            Assert.Throws<ArgumentException>(() => new Angle(5, -8));
        }

        [Fact]
        public void Constructor_NegativeNumerator_NormalizesCorrectly()
        {
            var angle = new Angle(-3, 8);

            Assert.Equal(5, angle.Numerator);
        }

        [Fact]
        public void EqualsObject_NullOrDifferentType_ReturnsFalse()
        {
            var angle = new Angle(1, 8);
            object? nullObj = null;
            object stringObj = "";

            Assert.False(angle.Equals(nullObj));
            Assert.False(angle.Equals(stringObj));
        }

        [Fact]
        public void ToString_ReturnsCorrectlyFormattedString()
        {
            var angle = new Angle(3, 8);

            var result = angle.ToString();

            Assert.Equal("(3, 8)", result);
        }
        [Fact]
        public void EqualsObject_EquivalentAngleBoxed_ReturnsTrue()
        {
            var angle1 = new Angle(15, 8);
            object angle2Boxed = new Angle(15, 8);

            var result = angle1.Equals(angle2Boxed);

            Assert.True(result);
        }

        [Fact]
        public void TwoAngles_DifferentDenominators_DoNotMutateEachOther()
        {
            var first = new Angle(1, 8);
            var second = new Angle(1, 16);

            Assert.Equal(8, first.Denominator);
            Assert.Equal(16, second.Denominator);
            Assert.Equal("(1, 8)", first.ToString());
            Assert.Equal("(1, 16)", second.ToString());
        }

        [Fact]
        public void Add_DifferentDenominators_ThrowsArgumentException()
        {
            var first = new Angle(1, 8);
            var second = new Angle(1, 16);

            Assert.Throws<ArgumentException>(() => first + second);
        }

        [Fact]
        public void Equals_DifferentDenominators_ReturnsFalse()
        {
            var first = new Angle(1, 8);
            var second = new Angle(1, 16);

            Assert.False(first.Equals(second));
            Assert.False(first == second);
            Assert.True(first != second);
            Assert.False(first.Equals((object)second));
        }

        [Fact]
        public void DefaultAngle_HasDefaultDenominatorAndZeroNumerator()
        {
            Angle angle = default;
            var explicitZero = new Angle(0, 8);

            Assert.Equal(0, angle.Numerator);
            Assert.Equal(8, angle.Denominator);
            Assert.Equal(0.0, (double)angle, 5);
            Assert.Equal(new Angle(1, 8), angle + new Angle(1, 8));
            Assert.Equal("(0, 8)", angle.ToString());
            Assert.True(angle.Equals(explicitZero));
            Assert.Equal(explicitZero.GetHashCode(), angle.GetHashCode());
        }

        [Fact]
        public void Add_LargeDenominators_DoesNotOverflowInt()
        {
            var first = new Angle(1_500_000_000, 2_000_000_000);
            var second = new Angle(1_500_000_000, 2_000_000_000);

            Assert.Equal(new Angle(1_000_000_000, 2_000_000_000), first + second);
        }
    }
}
