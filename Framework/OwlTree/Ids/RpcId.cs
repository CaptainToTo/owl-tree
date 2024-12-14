using System;

namespace OwlTree
{    
    public struct RpcId : IEncodable
    {
        // reserved rpc ids
        internal const UInt32 RPC_NONE = 0;
        internal const UInt32 CLIENT_CONNECTED_MESSAGE_ID       = 1;
        internal const UInt32 LOCAL_CLIENT_CONNECTED_MESSAGE_ID = 2;
        internal const UInt32 CLIENT_DISCONNECTED_MESSAGE_ID    = 3;
        internal const UInt32 NETWORK_OBJECT_SPAWN              = 4;
        internal const UInt32 NETWORK_OBJECT_DESPAWN            = 5;
        internal const UInt32 CONNECTION_REQUEST                = 6;
        internal const UInt32 HOST_MIGRATION                    = 7;
        internal const UInt32 PING_REQUEST                      = 8;

        public static RpcId None = new RpcId(RPC_NONE);

        /// <summary>
        /// The first valid RpcId value that isn't reserved for specific operations handled by OwlTree.
        /// </summary>
        public const int FIRST_RPC_ID = 10;

        /// <summary>
        /// Basic function signature for passing RpcIds.
        /// </summary>
        public delegate void Delegate(RpcId id);

        // tracks the current id for the next id generated
        private static UInt32 _curId = FIRST_RPC_ID;

        // the actual id
        private UInt32 _id;

        /// <summary>
        /// Generate a new rpc id.
        /// </summary>
        public static RpcId New()
        {
            return new RpcId(_curId);
        }

        /// <summary>
        /// Get a RpcId instance using an existing id.
        /// </summary>
        public RpcId(uint id)
        {
            _id = id;
            if (id >= _curId)
                _curId = (UInt32)(id + 1);
        }

        /// <summary>
        /// Get a RpcId instance by decoding it from a byte array.
        /// </summary>
        public RpcId(byte[] bytes)
        {
            _id = 0;
            FromBytes(bytes);
        }

        /// <summary>
        /// Get a RpcId instance by decoding it from a span.
        /// </summary>
        public RpcId(ReadOnlySpan<byte> bytes)
        {
            _id = 0;
            FromBytes(bytes);
        }

        /// <summary>
        /// The id value.
        /// </summary>
        public uint Id { get { return _id; } } 

        /// <summary>
        /// Gets the rpc id from the given bytes.
        /// </summary>
        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < 4)
                throw new ArgumentException("Span must have 4 bytes to decode a RpcId from.");

            var result = BitConverter.ToUInt32(bytes);

            _id = result;
            if (_id >= _curId)
                _curId = (UInt32)(_id + 1);
        }

        public static int MaxLength()
        {
            return 4;
        }

        public int ByteLength()
        {
            return 4;
        }

        /// <summary>
        /// Inserts id as bytes into the given span.
        /// </summary>
        public void InsertBytes(Span<byte> bytes)
        {
            if (bytes.Length < 4)
                return;
            BitConverter.TryWriteBytes(bytes, _id);
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

        public static implicit operator uint(RpcId id)
        {
            return id._id;
        } 

        public override bool Equals(object obj)
        {
            return obj != null && obj.GetType() == typeof(RpcId) && ((RpcId)obj)._id == _id;
        }

        public override int GetHashCode()
        {
            return _id.GetHashCode();
        }

    }
}