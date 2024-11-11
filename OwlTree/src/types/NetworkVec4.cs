
using System;

namespace OwlTree
{
    /// <summary>
    /// Vector4 struct that implements IEncodable.
    /// </summary>
    public struct NetworkVec4 : IEncodable
    {

        public float x;
        public float y;
        public float z;
        public float w;

        /// <summary>
        /// Create a new vector4 with the given x, y, z, and w component.
        /// </summary>
        public NetworkVec4(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        /// <summary>
        /// Create a new vector4 with the same x, y, z, and w values as the given vector4.
        /// </summary>
        public NetworkVec4(NetworkVec4 a)
        {
            x = a.x;
            y = a.y;
            z = a.z;
            w = a.w;
        }

        /// <summary>
        /// A vector4 with all components set to 0. (0, 0, 0, 0)
        /// </summary>
        public static NetworkVec4 Zero = new NetworkVec4(0, 0, 0, 0);

        /// <summary>
        /// A vector4 with all components set to 1. (1, 1, 1, 1)
        /// </summary>
        public static NetworkVec4 One = new NetworkVec4(1, 1, 1, 1);

        // utilities

        /// <summary>
        /// The magnitude (distance from (0, 0)) of this vector.
        /// </summary>
        public float Magnitude() => (float)Math.Sqrt(SqrMagnitude());

        /// <summary>
        /// The magnitude of this vector squared.
        /// </summary>
        public float SqrMagnitude() => (x * x) + (y * y) + (z * z) + (w * w);

        // encoding

        public int ByteLength() => 16;

        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            x = BitConverter.ToSingle(bytes);
            y = BitConverter.ToSingle(bytes.Slice(4));
            z = BitConverter.ToSingle(bytes.Slice(8));
            w = BitConverter.ToSingle(bytes.Slice(12));
        }

        public void InsertBytes(Span<byte> bytes)
        {
            BitConverter.TryWriteBytes(bytes, x);
            BitConverter.TryWriteBytes(bytes.Slice(4), y);
            BitConverter.TryWriteBytes(bytes.Slice(8), z);
            BitConverter.TryWriteBytes(bytes.Slice(12), w);
        }

        // operators

        public static bool operator ==(NetworkVec4 a, NetworkVec4 b)
        {
            return a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w;
        }

        public static bool operator !=(NetworkVec4 a, NetworkVec4 b)
        {
            return a.x != b.x || a.y != b.y || a.z != b.z || a.w != b.w;
        }

        public override bool Equals(object obj) => base.Equals(obj);

        public override int GetHashCode() => base.GetHashCode();

        public static NetworkVec4 operator +(NetworkVec4 a, NetworkVec4 b)
        {
            return new NetworkVec4(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w);
        }

        public static NetworkVec4 operator -(NetworkVec4 a, NetworkVec4 b)
        {
            return new NetworkVec4(a.x - b.x, a.y - b.y, a.z - b.z, a.w - b.w);
        }

        public static NetworkVec4 operator *(NetworkVec4 a, float b)
        {
            return new NetworkVec4(a.x * b, a.y * b, a.z * b, a.w * b);
        }

        public static NetworkVec4 operator /(NetworkVec4 a, float b)
        {
            return new NetworkVec4(a.x / b, a.y / b, a.z / b, a.w / b);
        }

        public override string ToString()
        {
            return "(" + x + ", " + y + ", " + z + ", " + w + ")";
        }
    }

}