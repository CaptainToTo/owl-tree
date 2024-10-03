namespace OwlTree
{
    public struct RpcId : IEncodable
    {
        // reserved rpc ids
        internal const ushort RPC_NONE = 0;
        internal const ushort CLIENT_CONNECTED_MESSAGE_ID       = 1;
        internal const ushort LOCAL_CLIENT_CONNECTED_MESSAGE_ID = 2;
        internal const ushort CLIENT_DISCONNECTED_MESSAGE_ID    = 3;
        internal const ushort NETWORK_OBJECT_SPAWN              = 4;
        internal const ushort NETWORK_OBJECT_DESPAWN            = 5;

        public static RpcId None = new RpcId(RPC_NONE);

        public static void InsertBytes(byte[] bytes, int ind, ushort rpcId)
        {
            BitConverter.TryWriteBytes(bytes.AsSpan(ind), rpcId);
        }

        internal const ushort FIRST_RPC_ID = 10;

        /// <summary>
        /// Basic function signature for passing RpcIds.
        /// </summary>
        public delegate void Delegate(RpcId id);

        // tracks the current id for the next id generated
        private static UInt16 _curId = FIRST_RPC_ID;

        // the actual id
        private UInt16 _id;

        /// <summary>
        /// Generate a new rpc id.
        /// </summary>
        public RpcId()
        {
            _id = _curId;
            _curId++;
        }

        /// <summary>
        /// Get a RpcId instance using an existing id.
        /// </summary>
        public RpcId(ushort id)
        {
            _id = id;
            if (id >= _curId)
                _curId = (ushort)(id + 1);
        }

        /// <summary>
        /// Get a RpcId instance by decoding it from a byte array.
        /// </summary>
        public RpcId(byte[] bytes)
        {
            FromBytes(bytes);
        }

        /// <summary>
        /// Get a RpcId instance by decoding it from a byte array, starting at ind.
        /// </summary>
        public RpcId(ReadOnlySpan<byte> bytes)
        {
            FromBytes(bytes);
        }

        public ushort Id { get { return _id; } } 

        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < 2)
                throw new ArgumentException("Byte array must have 2 bytes from ind to decode a RpcId from.");

            var result = BitConverter.ToUInt16(bytes);

            _id = result;
            if (_id >= _curId)
                _curId = (ushort)(_id + 1);
        }

        public static int MaxLength()
        {
            return 2;
        }

        public int ExpectedLength()
        {
            return 2;
        }

        public bool InsertBytes(Span<byte> bytes)
        {
            if (bytes.Length < 2)
                return false;
            BitConverter.TryWriteBytes(bytes, _id);
            return true;
        }

        // Operators

        /// <summary>
        /// Returns the id number as a string.
        /// </summary>
        public override string ToString()
        {
            return "<RpcId: " + _id.ToString() + ">";
        }

        public static bool operator ==(RpcId a, RpcId b)
        {
            return a._id == b._id;
        }

        public static bool operator !=(RpcId a, RpcId b)
        {
            return a._id != b._id;
        }

        public static implicit operator ushort(RpcId id)
        {
            return id._id;
        } 

        public override bool Equals(object? obj)
        {
            return obj != null && obj.GetType() == typeof(RpcId) && ((RpcId)obj)._id == _id;
        }

        public override int GetHashCode()
        {
            return _id.GetHashCode();
        }

    }
}