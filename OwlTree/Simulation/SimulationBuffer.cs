using System;

namespace OwlTree
{
    /// <summary>
    /// Stores incoming and outgoing messages. Control simulation tick behavior 
    /// through the message providers used by the rest of the connection.
    /// </summary>
    public abstract class SimulationBuffer
    {
        protected Logger _logger;

        public SimulationBuffer(Logger logger)
        {
            _logger = logger;
        }

        private readonly object _lock = new();

        /// <summary>
        /// The current tick the simulation is on. All outgoing and incoming messages 
        /// that will be provided at any given moment belong to this tick.
        /// </summary>
        public Tick CurTick { get; internal set; } = new Tick(0);

        protected abstract void InitBufferInternal(int tickRate, int latency, uint curTick, ClientId localId, bool isAuthority);

        /// <summary>
        /// Provide the agreed session tick rate, and local latency once these are known
        /// to initialize the buffer with an adequate amount of space.
        /// The current tick should be received from the session authority to start the
        /// simulation at the same tick as the authority.
        /// </summary>
        public void InitBuffer(int tickRate, int latency, uint curTick, ClientId localId, bool isAuthority)
        {
            lock (_lock)
            {
                InitBufferInternal(tickRate, latency, curTick, localId, isAuthority);
            }
        }

        protected abstract void UpdateAuthorityInternal(bool isAuthority);

        public void UpdateAuthority(bool isAuthority)
        {
            lock (_lock)
            {
                UpdateAuthorityInternal(isAuthority);
            }
        }

        protected abstract void AddTickSourceInternal(ClientId client);

        /// <summary>
        /// Start tracking simulation tick messages from this source.
        /// </summary>
        public void AddTickSource(ClientId client)
        {
            lock (_lock)
            {
                AddTickSourceInternal(client);
            }
        }

        protected abstract void RemoveTickSourceInternal(ClientId client);

        /// <summary>
        /// Stop tracking simulation tick messages from this source.
        /// </summary>
        public void RemoveTickSource(ClientId client)
        {
            lock (_lock)
            {
                RemoveTickSourceInternal(client);
            }
        }

        protected abstract bool HasOutgoingInternal();

        /// <summary>
        /// Messages are currently waiting to be sent.
        /// </summary>
        public bool HasOutgoing()
        {
            lock (_lock)
            {
                return HasOutgoingInternal();
            }
        }

        protected abstract void NextTickInternal();

        /// <summary>
        /// Move the simulation to the next tick.
        /// </summary>
        public void NextTick()
        {
            lock (_lock)
            {
                NextTickInternal();
            }
        }

        protected abstract void AddOutgoingInternal(OutgoingMessage m);

        /// <summary>
        /// Add a new, encoded outgoing message.
        /// </summary>
        public void AddOutgoing(OutgoingMessage m)
        {
            lock (_lock)
            {
                AddOutgoingInternal(m);
            }
        }

        protected abstract bool TryGetNextOutgoingInternal(out OutgoingMessage m);

        /// <summary>
        /// Try to get the next outgoing message, returns false if queue is empty.
        /// </summary>
        public bool TryGetNextOutgoing(out OutgoingMessage m)
        {
            lock (_lock)
            {
                return TryGetNextOutgoingInternal(out m);
            }
        }

        protected abstract void AddIncomingInternal(IncomingMessage m);

        /// <summary>
        /// Add a new, decoded incoming message.
        /// </summary>
        public void AddIncoming(IncomingMessage m)
        {
            lock (_lock)
            {
                AddIncomingInternal(m);
            }
        }

        protected abstract bool TryGetNextIncomingInternal(out IncomingMessage m);

        /// <summary>
        /// Try to get the next incoming message, returns false if queue is empty.
        /// </summary>
        public bool TryGetNextIncoming(out IncomingMessage m)
        {
            lock (_lock)
            {
                return TryGetNextIncomingInternal(out m);
            }
        }

        // RPCs ================

        public static int TickMessageLength => RpcId.MaxByteLength + ClientId.MaxByteLength + Tick.MaxByteLength + 8;

        public static void EncodeNextTick(Span<byte> bytes, ClientId source, Tick nextTick)
        {
            new RpcId(RpcId.NextTickId).InsertBytes(bytes);
            source.InsertBytes(bytes.Slice(RpcId.MaxByteLength));
            nextTick.InsertBytes(bytes.Slice(RpcId.MaxByteLength + ClientId.MaxByteLength));
            BitConverter.TryWriteBytes(bytes.Slice(Tick.MaxByteLength + RpcId.MaxByteLength + ClientId.MaxByteLength), 
                DateTimeOffset.Now.ToUnixTimeMilliseconds());
        }

        public static void EncodeCurTick(Span<byte> bytes, ClientId source, Tick curTick)
        {
            new RpcId(RpcId.CurTickId).InsertBytes(bytes);
            source.InsertBytes(bytes.Slice(RpcId.MaxByteLength));
            curTick.InsertBytes(bytes.Slice(RpcId.MaxByteLength + ClientId.MaxByteLength));
            BitConverter.TryWriteBytes(bytes.Slice(Tick.MaxByteLength + RpcId.MaxByteLength + ClientId.MaxByteLength), 
                DateTimeOffset.Now.ToUnixTimeMilliseconds());
        }

        public static void EncodeEndTick(Span<byte> bytes, ClientId source, Tick prevTick)
        {
            new RpcId(RpcId.EndTickId).InsertBytes(bytes);
            source.InsertBytes(bytes.Slice(RpcId.MaxByteLength));
            prevTick.InsertBytes(bytes.Slice(RpcId.MaxByteLength + ClientId.MaxByteLength));
            BitConverter.TryWriteBytes(bytes.Slice(Tick.MaxByteLength + RpcId.MaxByteLength + ClientId.MaxByteLength), 
                DateTimeOffset.Now.ToUnixTimeMilliseconds());
        }

        public static bool TryDecodeTickMessage(ReadOnlySpan<byte> bytes, out RpcId rpc, out ClientId source, out Tick tick, out long timestamp)
        {
            rpc = new RpcId(bytes);
            switch(rpc.Id)
            {
                case RpcId.NextTickId:
                case RpcId.CurTickId:
                case RpcId.EndTickId:
                    source = new ClientId(bytes.Slice(rpc.ByteLength()));
                    tick = new Tick(bytes.Slice(rpc.ByteLength() + source.ByteLength()));
                    timestamp = BitConverter.ToInt64(bytes.Slice(rpc.ByteLength() + source.ByteLength() + tick.ByteLength()));
                    return true;

                default:
                    source = ClientId.None;
                    tick = new Tick(0);
                    timestamp = 0;
                    return false;
            }
        }
    }
}