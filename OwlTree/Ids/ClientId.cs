
using System;

namespace OwlTree
{
    /// <summary>
    /// Unique integer Id for each client connected to the server. Ids are unique for each connection.
    /// This means if a client disconnects and then reconnects, their ClientId will be different.
    /// </summary>
    public struct ClientId : IEncodable
    {
        public const int MaxByteLength = 4;

        /// <summary>
        /// Basic function signature for passing ClientIds.
        /// </summary>
        public delegate void Delegate(ClientId id);

        public const UInt32 FirstClientId = 1;

        // the actual id
        private UInt32 _id;

        /// <summary>
        /// Get a ClientId instance using an existing id.
        /// </summary>
        public ClientId(uint id)
        {
            _id = id;
        }

        /// <summary>
        /// Get a ClientId instance by decoding it from a span.
        /// </summary>
        public ClientId(ReadOnlySpan<byte> bytes)
        {
            _id = 0;
            FromBytes(bytes);
        }

        /// <summary>
        /// The id value.
        /// </summary>
        public uint Id => _id;

        /// <summary>
        /// Gets the client id from the given bytes.
        /// </summary>
        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < MaxByteLength)
                throw new ArgumentException($"Span must have {MaxByteLength} bytes to decode a ClientId from.");

            _id = BitConverter.ToUInt32(bytes);
        }

        /// <summary>
        /// Inserts id as bytes into the given span.
        /// </summary>
        public void InsertBytes(Span<byte> bytes)
        {
            if (bytes.Length < MaxByteLength)
                return;
            BitConverter.TryWriteBytes(bytes, _id);
        }

        public int ByteLength() => MaxByteLength;

        /// <summary>
        /// The client id used to signal that there is no client. Id value is 0.
        /// </summary>
        public static ClientId None = new ClientId(0);

        // Operators

        /// <summary>
        /// Returns the id number as a string.
        /// </summary>
        public override string ToString()
        {
            return "<ClientId: " + (_id == 0 ? "None" : _id.ToString()) + ">";
        }

        public static bool operator ==(ClientId a, ClientId b)
        {
            return a._id == b._id;
        }

        public static bool operator !=(ClientId a, ClientId b)
        {
            return a._id != b._id;
        }

        public override bool Equals(object obj)
        {
            return obj != null && obj.GetType() == typeof(ClientId) && ((ClientId)obj)._id == _id;
        }

        public override int GetHashCode()
        {
            return _id.GetHashCode();
        }
    }
}