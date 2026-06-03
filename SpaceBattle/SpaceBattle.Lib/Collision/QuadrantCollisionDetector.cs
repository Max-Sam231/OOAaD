using System;
using System.Linq;

namespace SpaceBattle.Lib
{
    public class QuadrantCollisionDetector : ICollisionDetector
    {
        private readonly int _quadrantSize;

        public QuadrantCollisionDetector(int quadrantSize)
        {
            if (quadrantSize <= 0)
                throw new ArgumentException("Quadrant size must be positive.", nameof(quadrantSize));

            _quadrantSize = quadrantSize;
        }

        public bool DetectCollision(ICollidable a, ICollidable b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));

            var posA = a.Position ?? throw new InvalidOperationException("Position of object A is null.");
            var posB = b.Position ?? throw new InvalidOperationException("Position of object B is null.");

            var quadrantA = GetQuadrant(posA);
            var quadrantB = GetQuadrant(posB);

            return quadrantA == quadrantB;
        }

        private Vector GetQuadrant(Vector position)
        {
            var coordinates = Enumerable.Range(0, position.Dimension)
                .Select(i => (int)Math.Floor((double)position[i] / _quadrantSize))
                .ToArray();

            return new Vector(coordinates);
        }
    }
}
