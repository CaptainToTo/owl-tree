using System;

namespace OwlTree
{    
    public struct RpcId : IEncodable
    {
        public const int MaxByteLength = 4;

        // reserved rpc ids
        internal const UInt32 NoneId                  = 0;
        internal const UInt32 ClientConnectedId       = 1;
        internal const UInt32 LocalClientConnectedId  = 2;
        internal const UInt32 ClientDisconnectedId    = 3;
        internal const UInt32 NetworkObjectSpawnId    = 4;
        internal const UInt32 NetworkObjectDespawnId  = 5;
        internal const UInt32 ConnectionRequestId     = 6;
        internal const UInt32 HostMigrationId         = 7;
        internal const UInt32 PingRequestId           = 8;
        internal const UInt32 NextTickId              = 9;
        internal const UInt32 CurTickId               = 10;
        internal const UInt32 EndTickId               = 11;

        internal const UInt32 LocalReadyId            = 15;
        
        /// <summary>
        /// Returns true if the given id is for a client event, such as connecting or disconnecting.
        /// </summary>
        internal bool IsClientEvent()
        {
            return _id == ClientConnectedId || _id == LocalClientConnectedId || _id == ClientDisconnectedId ||
                _id == HostMigrationId || _id == LocalReadyId;
        }

        internal bool IsObjectEvent()
        {
            return _id == NetworkObjectSpawnId || _id == NetworkObjectDespawnId;
        }

        internal bool IsTickEvent()
        {
            return _id == NextTickId || _id == CurTickId || _id == EndTickId;
        }

        internal static string SpecialIdToString(RpcId id)
        {
            switch (id)
            {
                case NoneId: return "<RpcId: None>";
                case ClientConnectedId: return "Client Connected";
                case LocalClientConnectedId: return "Local Client Connected";
                case ClientDisconnectedId: return "Client Disconnected";
                case NetworkObjectSpawnId: return "NetworkObject Spawned";
                case NetworkObjectDespawnId: return "NetworkObject Despawned";
                case ConnectionRequestId: return "Connection Request";
                case HostMigrationId: return "Host Migration";
                case PingRequestId: return "Ping Request";
                case NextTickId: return "Next Tick";
                case CurTickId: return "Current Tick";
                case EndTickId: return "End Tick";
                case LocalReadyId: return "Local Connection Ready";
                default: return id.ToString();
            }
        }

        /// <summary>
        /// Represent a non-existent RPC.
        /// </summary>
        public static RpcId None = new RpcId(NoneId);

        /// <summary>
        /// The first valid RpcId value that isn't reserved for specific operations handled by OwlTree.
        /// </summary>
        public const int FirstRpcId = 30;

        /// <summary>
        /// Basic function signature for passing RpcIds.
        /// </summary>
        public delegate void Delegate(RpcId id);

        // the actual id
        private UInt32 _id;

        /// <summary>
        /// Get a RpcId instance using an existing id.
        /// </summary>
        public RpcId(uint id)
        {
            _id = id;
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
        public uint Id => _id;

        /// <summary>
        /// Gets the rpc id from the given bytes.
        /// </summary>
        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < MaxByteLength)
                throw new ArgumentException($"Span must have {MaxByteLength} bytes to decode a RpcId from.");

            var result = BitConverter.ToUInt32(bytes);

            _id = result;
        }

        public int ByteLength() => MaxByteLength;

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