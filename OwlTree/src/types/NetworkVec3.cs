
using System;

namespace OwlTree
{
    /// <summary>
    /// Vector3 struct that implements IEncodable.
    /// </summary>
    public struct NetworkVec3 : IEncodable
    {

        public float x;
        public float y;
        public float z;

        /// <summary>
        /// Create a new vector3 with the given x, y, and z component.
        /// </summary>
        public NetworkVec3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        /// <summary>
        /// Create a new vector3 with the same x, y, and z values as the given vector3.
        /// </summary>
        public NetworkVec3(NetworkVec3 a)
        {
            x = a.x;
            y = a.y;
            z = a.z;
        }

        /// <summary>
        /// A vector3 with all components set to 0. (0, 0, 0)
        /// </summary>
        public static NetworkVec3 Zero = new NetworkVec3(0, 0, 0);

        /// <summary>
        /// A vector3 with all components set to 1. (1, 1, 1)
        /// </summary>
        public static NetworkVec3 One = new NetworkVec3(1, 1, 1);

        // utilities

        /// <summary>
        /// The magnitude (distance from (0, 0)) of this vector.
        /// </summary>
        public float Magnitude() => (float)Math.Sqrt(SqrMagnitude());

        /// <summary>
        /// The magnitude of this vector squared.
        /// </summary>
        public float SqrMagnitude() => (x * x) + (y * y) + (z * z);

        // encoding

        public int ByteLength() => 12;

        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            x = BitConverter.ToSingle(bytes);
            y = BitConverter.ToSingle(bytes.Slice(4));
            z = BitConverter.ToSingle(bytes.Slice(8));
        }

        public void InsertBytes(Span<byte> bytes)
        {
            BitConverter.TryWriteBytes(bytes, x);
            BitConverter.TryWriteBytes(bytes.Slice(4), y);
            BitConverter.TryWriteBytes(bytes.Slice(8), z);
        }

        // operators

        public static bool operator ==(NetworkVec3 a, NetworkVec3 b)
        {
            return a.x == b.x && a.y == b.y && a.z == b.z;
        }

        public static bool operator !=(NetworkVec3 a, NetworkVec3 b)
        {
            return a.x != b.x || a.y != b.y || a.z != b.z;
        }

        public override bool Equals(object obj) => base.Equals(obj);

        public override int GetHashCode() => base.GetHashCode();

        public static NetworkVec3 operator +(NetworkVec3 a, NetworkVec3 b)
        {
            return new NetworkVec3(a.x + b.x, a.y + b.y, a.z + b.z);
        }

        public static NetworkVec3 operator -(NetworkVec3 a, NetworkVec3 b)
        {
            return new NetworkVec3(a.x - b.x, a.y - b.y, a.z - b.z);
        }

        public static NetworkVec3 operator *(NetworkVec3 a, float b)
        {
            return new NetworkVec3(a.x * b, a.y * b, a.z * b);
        }

        public static NetworkVec3 operator /(NetworkVec3 a, float b)
        {
            return new NetworkVec3(a.x / b, a.y / b, a.z / b);
        }

        public override string ToString()
        {
            return "(" + x + ", " + y + ", " + z + ")";
        }
    }

}