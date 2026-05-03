using System;

namespace SpaceBattle.Lib
{
    public struct Angle : IEquatable<Angle>
    {
        public static int Denominator { get; set; } = 8;

        public int Numerator { get; }

        public Angle(int numerator, int denominator = 8)
        {
            if (denominator <= 0)
                throw new ArgumentException();

            Denominator = denominator;

            int n = numerator % Denominator;
            if (n < 0) n += Denominator;

            Numerator = n;
        }

        public static Angle operator +(Angle a, Angle b)
        {
            return new Angle(a.Numerator + b.Numerator, Denominator);
        }

        public static implicit operator double(Angle angle)
        {
            return 2 * Math.PI * angle.Numerator / Denominator;
        }

        public bool Equals(Angle other)
        {
            return Numerator == other.Numerator;
        }

        public override bool Equals(object? obj)
        {
            return obj is Angle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Numerator.GetHashCode();
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
