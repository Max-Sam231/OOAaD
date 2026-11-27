using System;

namespace SpaceBattle.Lib
{
    public readonly struct Angle : IEquatable<Angle>
    {
        private const int DefaultDenominator = 8;
        private readonly int _denominator;

        public int Numerator { get; }

        public int Denominator => _denominator == 0 ? DefaultDenominator : _denominator;

        public Angle(int numerator, int denominator = DefaultDenominator)
        {
            if (denominator <= 0)
            {
                throw new ArgumentException("Denominator must be positive.", nameof(denominator));
            }

            _denominator = denominator;

            int n = numerator % denominator;
            if (n < 0)
            {
                n += denominator;
            }

            Numerator = n;
        }

        public static Angle operator +(Angle a, Angle b)
        {
            if (a.Denominator != b.Denominator)
            {
                throw new ArgumentException("Cannot add angles with different denominators.");
            }

            return new Angle((int)(((long)a.Numerator + b.Numerator) % a.Denominator), a.Denominator);
        }

        public static implicit operator double(Angle angle)
        {
            return 2 * Math.PI * angle.Numerator / angle.Denominator;
        }

        public bool Equals(Angle other)
        {
            return Numerator == other.Numerator && Denominator == other.Denominator;
        }

        public override bool Equals(object? obj)
        {
            return obj is Angle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Numerator, Denominator);
        }

        public static bool operator ==(Angle left, Angle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Angle left, Angle right)
        {
            return !(left == right);
        }

        public override string ToString()
        {
            return $"({Numerator}, {Denominator})";
        }
    }
}
