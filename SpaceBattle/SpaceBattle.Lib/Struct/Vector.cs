using System;
using System.Linq;

namespace SpaceBattle.Lib
{
    public class Vector
    {
        private readonly int[] _coordinates;

        public Vector(params int[] coordinates)
        {
            _coordinates = coordinates ?? Array.Empty<int>();
        }

        public static Vector operator +(Vector a, Vector b)
        {
            if (a._coordinates.Length != b._coordinates.Length)
            {
                throw new ArgumentException();
            }

            var result = a._coordinates.Zip(b._coordinates, (x, y) => x + y).ToArray();

            return new Vector(result);
        }

        public override bool Equals(object? obj)
        {
            if (obj is Vector other)
            {
                return _coordinates.SequenceEqual(other._coordinates);
            }
            return false;
        }

        public override int GetHashCode() =>
            _coordinates.Aggregate(17, (hash, coordinate) => unchecked(hash * 31 + coordinate.GetHashCode()));

        public static bool operator ==(Vector a, Vector b) =>
            a is null ? b is null : a.Equals(b);

        public static bool operator !=(Vector a, Vector b)
        {
            return !(a == b);
        }
    }
}
