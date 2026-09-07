using System;

namespace TrollStrategy.Domain
{
    [Serializable]
    public struct Cell : IEquatable<Cell>
    {
        public int X;
        public int Y;

        public Cell(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(Cell other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Cell other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X}, {Y})";

        public static bool operator ==(Cell left, Cell right) => left.Equals(right);
        public static bool operator !=(Cell left, Cell right) => !left.Equals(right);
    }

    [Serializable]
    public struct WorldPosition : IEquatable<WorldPosition>
    {
        public float X;
        public float Y;

        public WorldPosition(float x, float y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(WorldPosition other) => Math.Abs(X - other.X) < 0.0001f && Math.Abs(Y - other.Y) < 0.0001f;
        public override bool Equals(object obj) => obj is WorldPosition other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X:F2}, {Y:F2})";
    }
}
