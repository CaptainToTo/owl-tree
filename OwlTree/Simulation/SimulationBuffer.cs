using System;

namespace OwlTree
{
    /// <summary>
    /// Stores incoming and outgoing messages. Control simulation tick behavior 
    /// through the message providers used by the rest of the connection.
    /// </summary>
    public abstract class SimulationBuffer
    {
        protected class TickPair
        {
            public Tick Tcp;
            public Tick Udp;

            public TickPair(Tick tcp, Tick udp)
            {
                Tcp = tcp;
                Udp = udp;
            }

            public Tick Select(Protocol protocol) => protocol == Protocol.Tcp ? Tcp : Udp;

            public void Update(Protocol protocol, Tick tick)
            {
                if (protocol == Protocol.Tcp)
                    Tcp = tick;
                else
                    Udp = tick;
            }

            public Tick Min() => Tcp < Udp ? Tcp : Udp;
            public Tick Max() => Tcp > Udp ? Tcp : Udp;
        }


        protected Logger _logger;

        public SimulationBuffer(Logger logger)
        {
            _logger = logger;
        }

        private readonly object _lock = new();

        /// <summary>
        /// The current tick the simulation is on. All outgoing messages 
        /// that will be provided at any given moment belong to this tick.
        /// </summary>
        public Tick LocalTick()
        {
            lock (_lock)
            {
                return _localTick;
            }
        }
        protected Tick _localTick = new Tick(0);
        
        /// <summary>
        /// The current tick received RPCs that are currently executing belong to.
        /// </summary>
        public Tick PresentTick()
        {
            lock (_lock)
            {
                return _presentTick;
            }
        }
        protected Tick _presentTick = new Tick(0);

        public Action<Tick> OnResimulation = null;

        protected abstract void InitBufferInternal(int tickRate, int latency, uint curTick, ClientId localId, ClientId authority);

        /// <summary>
        /// Provide the agreed session tick rate, and local latency once these are known
        /// to initialize the buffer with an adequate amount of space.
        /// The current tick should be received from the session authority to start the
        /// simulation at the same tick as the authority.
        /// </summary>
        public void InitBuffer(int tickRate, int latency, uint curTick, ClientId localId, ClientId authority)
        {
            lock (_lock)
            {
                InitBufferInternal(tickRate, latency, curTick, localId, authority);
            }
        }

        protected abstract void UpdateAuthorityInternal(ClientId authority);

        public void UpdateAuthority(ClientId authority)
        {
            lock (_lock)
            {
                UpdateAuthorityInternal(authority);
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

        

        public static string TickEncodingSummary(RpcId rpcId, ClientId source, ClientId callee, Tick tick, long timestamp, Protocol protocol)
        {
            string title = null;
            string sourceStr = source == ClientId.None ? "Server" : ("Client " + source.ToString());
            byte[] bytes = new byte[Encoder.TickMessageLength];
            switch (rpcId)
            {
                case RpcId.NextTickId:
                    title += $"{(protocol == Protocol.Tcp ? "TCP" : "UDP")} Next Tick message from {sourceStr}, updated to {tick} at {timestamp}:";
                    Encoder.EncodeNextTick(bytes, source, callee, tick, timestamp);
                    break;
                case RpcId.CurTickId:
                    title += $"Authority sent session tick of {tick} at {timestamp} to {callee}:";
                    Encoder.EncodeCurTick(bytes, source, callee, tick, timestamp);
                    break;
            }
            string bytesStr = "\n     Bytes: " + Encoder.ToString(bytes) + "\n";
            string encoding = "  Encoding: |__RpcId__| |_Caller__| |_Callee__| |__Tick___| |______Timestamp______|";
            return title + bytesStr + encoding;
        }

        public static string TickEncodingSummary(RpcId rpcId, ClientId source, ClientId callee, Tick tick, long timestamp)
        {
            string title = null;
            string sourceStr = source == ClientId.None ? "Server" : ("Client " + source.ToString());
            byte[] bytes = new byte[Encoder.TickMessageLength];
            switch (rpcId)
            {
                case RpcId.NextTickId:
                    title += $"Next Tick message from {sourceStr}, updated to {tick} at {timestamp}:";
                    Encoder.EncodeNextTick(bytes, source, callee, tick, timestamp);
                    break;
                case RpcId.CurTickId:
                    title += $"Authority sent session tick of {tick} at {timestamp} to {callee}:";
                    Encoder.EncodeCurTick(bytes, source, callee, tick, timestamp);
                    break;
            }
            string bytesStr = "\n     Bytes: " + Encoder.ToString(bytes) + "\n";
            string encoding = "  Encoding: |__RpcId__| |_Caller__| |_Callee__| |__Tick___| |______Timestamp______|";
            return title + bytesStr + encoding;
        }
    }
}