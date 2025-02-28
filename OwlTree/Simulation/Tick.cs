using System;

namespace OwlTree
{
    /// <summary>
    /// Represents the integer ID of a singular simulation tick. Can be compared against other ticks.
    /// Ticks are encodable.
    /// </summary>
    public struct Tick : IEncodable
    {
        /// <summary>
        /// General purpose delegate for passing ticks.
        /// </summary>
        public delegate void Delegate(Tick tick);

        private UInt32 _val;

        /// <summary>
        /// The integer value of this tick
        /// </summary>
        public uint Val => _val;

        /// <summary>
        /// Create a new tick that has the given tick number.
        /// </summary>
        public Tick(uint val)
        {
            _val = val;
        }

        /// <summary>
        /// Create a new tick by decoding it from a span of bytes.
        /// </summary>
        public Tick(ReadOnlySpan<byte> bytes)
        {
            _val = 0;
            FromBytes(bytes);
        }

        public Tick Next() => new Tick(_val + 1);

        public int ByteLength() => 4;

        public const int MaxByteLength = 4;

        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            _val = BitConverter.ToUInt32(bytes);
        }

        public void InsertBytes(Span<byte> bytes)
        {
            BitConverter.TryWriteBytes(bytes, _val);
        }

        public override string ToString() => "<Tick: " + _val + ">";

        public static bool operator ==(Tick a, Tick b) => a._val == b._val;
        public static bool operator !=(Tick a, Tick b) => a._val != b._val;

        public static bool operator <(Tick a, Tick b) => a._val < b._val;
        public static bool operator >(Tick a, Tick b) => a._val > b._val;

        public static bool operator >=(Tick a, Tick b) => a._val >= b._val;
        public static bool operator <=(Tick a, Tick b) => a._val <= b._val;

        public override bool Equals(object obj) => obj != null && obj.GetType() == typeof(Tick) && ((Tick)obj)._val == _val;
        public override int GetHashCode() => _val.GetHashCode();

        public static implicit operator uint(Tick a) => a._val;
    }
}