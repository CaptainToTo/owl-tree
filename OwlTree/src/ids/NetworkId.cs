using System;

namespace OwlTree
{
    /// <summary>
    /// Unique integer Id for each network object. 
    /// </summary>
    public struct NetworkId : IEncodable
    {
        // tracks the current id for the next id generated
        private static UInt32 _curId = 1;

        /// <summary>
        /// Reset ids. Provide an array of all current network object ids, which will be re-assigned to reduce the max id value.
        /// Use this for long-running servers which might run out available ids. Any NetworkIds that are stored as integers 
        /// will likely be inaccurate after a reset.<br />
        /// <br />
        /// newIds must be at least the same length as curIds.
        /// </summary>
        public static void ResetIdsNonAlloc(NetworkId[] curIds, ref NetworkId[] newIds)
        {
            if (curIds.Length > newIds.Length)
                throw new ArgumentException("The newIds array must be at least the same size as the curIds array.");

            _curId = 1;
            for (int i = 0; i < curIds.Length; i++)
            {
                newIds[i]._id = _curId;
                _curId++;
            }
        }

        /// <summary>
        /// Reset ids. Provide an array of all current network object ids, which will be re-assigned to reduce the max id value.
        /// Use this for long-running servers which might run out available ids. Any NetworkIds that are stored as integers 
        /// will likely be inaccurate after a reset.
        /// </summary>
        public static NetworkId[] ResetIds(NetworkId[] curIds)
        {
            NetworkId[] newIds = new NetworkId[curIds.Length];
            ResetIdsNonAlloc(curIds, ref newIds);
            return newIds;
        }

        // the actual id
        private UInt32 _id;

        /// <summary>
        /// Generate a new network object id.
        /// </summary>
        public static NetworkId New()
        {
            return new NetworkId(_curId);
        }

        /// <summary>
        /// Get a NetworkId instance using an existing id.
        /// </summary>
        public NetworkId(uint id)
        {
            _id = id;
            if (_id >= _curId)
                _curId = _id + 1;
        }

        /// <summary>
        /// Get a NetworkId instance by decoding it from a span.
        /// </summary>
        public NetworkId(ReadOnlySpan<byte> bytes)
        {
            _id = 0;
            FromBytes(bytes);
        }

        /// <summary>
        /// The id value.
        /// </summary>
        public uint Id { get { return _id; } }

        /// <summary>
        /// Gets the network id from the given bytes.
        /// </summary>
        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < 4)
                throw new ArgumentException("Span must have 4 bytes from ind to decode a ClientId from.");

            _id = BitConverter.ToUInt32(bytes);
            if (_id >= _curId)
                _curId = _id + 1;
        }

        /// <summary>
        /// Inserts id as bytes into the given span.
        /// Returns true if insertion was successful, false if there wasn't enough space in the span.
        /// </summary>
        public bool InsertBytes(Span<byte> bytes)
        {
            if (bytes.Length < 4)
                return false;
            BitConverter.TryWriteBytes(bytes, _id);
            return true;
        }

        public int ByteLength() { return 4; }

        /// <summary>
        /// The network object id used to signal that there is no object. Id value is 0.
        /// </summary>
        public static NetworkId None = new NetworkId(0);

        // Operators

        /// <summary>
        /// Returns the id number as a string.
        /// </summary>
        public override string ToString()
        {
            return "<NetworkId: " + (_id == 0 ? "None" : _id.ToString()) + ">";
        }

        public static bool operator ==(NetworkId a, NetworkId b)
        {
            return a._id == b._id;
        }

        public static bool operator !=(NetworkId a, NetworkId b)
        {
            return a._id != b._id;
        }

        public override bool Equals(object obj)
        {
            return obj != null && obj.GetType() == typeof(NetworkId) && ((NetworkId)obj)._id == _id;
        }

        public override int GetHashCode()
        {
            return _id.GetHashCode();
        }

        public static int MaxLength()
        {
            return 4;
        }
    }
}