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

            var result = new int[a._coordinates.Length];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = a._coordinates[i] + b._coordinates[i];
            }

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

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                foreach (var coordinate in _coordinates)
                {
                    hash = hash * 31 + coordinate.GetHashCode();
                }
                return hash;
            }
        }

        public static bool operator ==(Vector a, Vector b)
        {
            if (ReferenceEquals(a, null) && ReferenceEquals(b, null)) return true;
            if (ReferenceEquals(a, null) || ReferenceEquals(b, null)) return false;

            return a.Equals(b);
        }

        public static bool operator !=(Vector a, Vector b)
        {
            return !(a == b);
        }
    }
}
