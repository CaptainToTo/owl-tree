
using System;

namespace OwlTree
{
    /// <summary>
    /// Vector2 struct that implements IEncodable.
    /// </summary>
    public struct NetworkVec2 : IEncodable
    {

        public float x;
        public float y;

        /// <summary>
        /// Create a new vector2 with the given x and y component.
        /// </summary>
        public NetworkVec2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        /// <summary>
        /// Create a new vector2 with the same x and y values as the given vector2.
        /// </summary>
        public NetworkVec2(NetworkVec2 a)
        {
            x = a.x;
            y = a.y;
        }

        /// <summary>
        /// A vector2 with all components set to 0. (0, 0)
        /// </summary>
        public static NetworkVec2 Zero = new NetworkVec2(0, 0);

        /// <summary>
        /// A vector2 with all components set to 1. (1, 1)
        /// </summary>
        public static NetworkVec2 One = new NetworkVec2(1, 1);

        // utilities

        /// <summary>
        /// The magnitude (distance from (0, 0)) of this vector.
        /// </summary>
        public float Magnitude() => (float)Math.Sqrt(SqrMagnitude());

        /// <summary>
        /// The magnitude of this vector squared.
        /// </summary>
        public float SqrMagnitude() => (x * x) + (y * y);

        // encoding

        public int ByteLength() => 8;

        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            x = BitConverter.ToSingle(bytes);
            y = BitConverter.ToSingle(bytes.Slice(4));
        }

        public void InsertBytes(Span<byte> bytes)
        {
            BitConverter.TryWriteBytes(bytes, x);
            BitConverter.TryWriteBytes(bytes.Slice(4), y);
        }

        // operators

        public static bool operator ==(NetworkVec2 a, NetworkVec2 b)
        {
            return a.x == b.x && a.y == b.y;
        }

        public static bool operator !=(NetworkVec2 a, NetworkVec2 b)
        {
            return a.x != b.x && a.y != b.y;
        }

        public override bool Equals(object obj) => base.Equals(obj);

        public override int GetHashCode() => base.GetHashCode();

        public static NetworkVec2 operator +(NetworkVec2 a, NetworkVec2 b)
        {
            return new NetworkVec2(a.x + b.x, a.y + b.y);
        }

        public static NetworkVec2 operator -(NetworkVec2 a, NetworkVec2 b)
        {
            return new NetworkVec2(a.x - b.x, a.y - b.y);
        }

        public static NetworkVec2 operator *(NetworkVec2 a, float b)
        {
            return new NetworkVec2(a.x * b, a.y * b);
        }

        public static NetworkVec2 operator /(NetworkVec2 a, float b)
        {
            return new NetworkVec2(a.x / b, a.y / b);
        }

        public override string ToString()
        {
            return "(" + x + ", " + y + ")";
        }
    }

}