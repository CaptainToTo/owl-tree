
namespace OwlTree
{
    /// <summary>
    /// Unique integer Id for each client connected to the server. Ids are unique for each connection.
    /// This means if a client disconnects and then reconnects, their ClientId will be different.
    /// </summary>
    public struct ClientId : IEncodable
    {
        /// <summary>
        /// Basic function signature for passing ClientIds.
        /// </summary>
        public delegate void Delegate(ClientId id);

        // tracks the current id for the next id generated
        private static UInt32 _curId = 1;

        /// <summary>
        /// Reset ids. Provide an array of all current client ids, which will be re-assigned to reduce the max id value.
        /// Use this for long-running servers which might run out available ids. Any ClientIds that are stored as integers 
        /// will likely be inaccurate after a reset.<br />
        /// <br />
        /// newIds must be at least the same length as curIds.
        /// </summary>
        public static void ResetIdsNonAlloc(ClientId[] curIds, ref ClientId[] newIds)
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
        /// Reset ids. Provide an array of all current client ids, which will be re-assigned to reduce the max id value.
        /// Use this for long-running servers which might run out available ids. Any ClientIds that are stored as integers 
        /// will likely be inaccurate after a reset.
        /// </summary>
        public static ClientId[] ResetIds(ClientId[] curIds)
        {
            ClientId[] newIds = new ClientId[curIds.Length];
            ResetIdsNonAlloc(curIds, ref newIds);
            return newIds;
        }

        // the actual id
        private UInt32 _id;

        /// <summary>
        /// Generate a new client id.
        /// </summary>
        public ClientId()
        {
            _id = _curId;
            _curId++;
        }

        /// <summary>
        /// Get a ClientId instance using an existing id.
        /// </summary>
        public ClientId(uint id)
        {
            _id = id;
            if (id >= _curId)
                _curId = id + 1;
        }

        /// <summary>
        /// Get a ClientId instance by decoding it from a byte array.
        /// </summary>
        public ClientId(ReadOnlySpan<byte> bytes)
        {
            FromBytes(bytes);
        }

        /// <summary>
        /// The id value.
        /// </summary>
        public uint Id { get { return _id; } }

        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < 4)
                throw new ArgumentException("Byte array must have 4 bytes to decode a ClientId from.");

            _id = BitConverter.ToUInt32(bytes);
            if (_id >= _curId)
                _curId = _id + 1;
        }

        /// <summary>
        /// Inserts id as bytes into the given byte array, starting at ind.
        /// Returns true if insertion was successful, false if there wasn't enough space in the byte array.
        /// </summary>
        public bool InsertBytes(Span<byte> bytes)
        {
            if (bytes.Length < 4)
                return false;
            BitConverter.TryWriteBytes(bytes, _id);
            return true;
        }

        public int ExpectedLength() { return 4; }

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

        public override bool Equals(object? obj)
        {
            return obj != null && obj.GetType() == typeof(ClientId) && ((ClientId)obj)._id == _id;
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