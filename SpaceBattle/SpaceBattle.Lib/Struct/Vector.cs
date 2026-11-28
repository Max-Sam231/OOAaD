using System;

namespace SpaceBattle.Lib
{
    public sealed class Vector : IEquatable<Vector>
    {
        private readonly int[] _coordinates;

        public Vector(params int[] coordinates)
            : this(coordinates, true)
        {
        }

        private Vector(int[] coordinates, bool clone)
        {
            if (coordinates is null || coordinates.Length == 0)
            {
                _coordinates = Array.Empty<int>();
                return;
            }

            _coordinates = clone ? (int[])coordinates.Clone() : coordinates;
        }

        public int Dimension => _coordinates.Length;

        public int this[int index] => _coordinates[index];

        public static Vector operator +(Vector a, Vector b)
        {
            ArgumentNullException.ThrowIfNull(a);
            ArgumentNullException.ThrowIfNull(b);

            if (a._coordinates.Length != b._coordinates.Length)
            {
                throw new ArgumentException();
            }

            var result = new int[a._coordinates.Length];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = a._coordinates[i] + b._coordinates[i];
            }

            return new Vector(result, false);
        }

        public bool Equals(Vector? other)
        {
            if (other is null)
            {
                return false;
            }

            if (_coordinates.Length != other._coordinates.Length)
            {
                return false;
            }

            for (var i = 0; i < _coordinates.Length; i++)
            {
                if (_coordinates[i] != other._coordinates[i])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as Vector);
        }

        public override int GetHashCode()
        {
            var hash = 17;
            for (var i = 0; i < _coordinates.Length; i++)
            {
                hash = unchecked(hash * 31 + _coordinates[i]);
            }

            return hash;
        }

        public static bool operator ==(Vector? left, Vector? right)
        {
            return ReferenceEquals(left, right) || (left is not null && left.Equals(right));
        }

        public static bool operator !=(Vector? left, Vector? right)
        {
            return !(left == right);
        }
    }
}
