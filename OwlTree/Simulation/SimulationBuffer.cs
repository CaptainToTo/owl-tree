using System;
using Priority_Queue;

namespace OwlTree
{
    /// <summary>
    /// Stores incoming and outgoing messages. Control simulation tick behavior 
    /// through the message providers used by the rest of the connection.
    /// </summary>
    internal abstract class SimulationBuffer
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


        protected Logger Logger;
        protected IClientRegistry ClientRegistry;
        protected IReplicator Replicator;

        public SimulationBuffer(Logger logger, IClientRegistry registry, IReplicator replicator)
        {
            Logger = logger;
            ClientRegistry = registry;
            Replicator = replicator;
        }

        private readonly object _lock = new();

        /// <summary>
        /// The current tick the simulation is on. All outgoing messages 
        /// that will be provided at any given moment belong to this tick.
        /// </summary>
        public Tick GetLocalTick()
        {
            lock (_lock)
            {
                return LocalTick;
            }
        }
        protected Tick LocalTick = new Tick(0);
        
        /// <summary>
        /// The current tick received RPCs that are currently executing belong to.
        /// </summary>
        public Tick GetPresentTick()
        {
            lock (_lock)
            {
                return PresentTick;
            }
        }
        protected Tick PresentTick = new Tick(0);

        public Action<Tick> OnResimulation = null;

        protected abstract void InitBufferInternal(int tickRate, int latency, uint curTick);

        /// <summary>
        /// Provide the agreed session tick rate, and local latency once these are known
        /// to initialize the buffer with an adequate amount of space.
        /// The current tick should be received from the session authority to start the
        /// simulation at the same tick as the authority.
        /// </summary>
        public void InitBuffer(int tickRate, int latency, uint curTick)
        {
            lock (_lock)
            {
                InitBufferInternal(tickRate, latency, curTick);
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

        // Helpers =============

        protected static Tick CompensateForLatency(Tick presentTick, int latency, int tickRate)
        {
            return new Tick(presentTick.Value + (uint)MathF.Ceiling((float)latency / tickRate));
        }

        protected void SendNextTick(SimplePriorityQueue<OutgoingMessage, uint> q)
        {
            var localId = ClientRegistry.GetLocalId();

            var tickTcpMessage = new OutgoingMessage
            {
                tick = LocalTick,
                caller = localId,
                callee = ClientId.None,
                rpcId = new RpcId(RpcId.NextTickId),
                target = NetworkId.None,
                protocol = Protocol.Tcp,
                perms = RpcPerms.AnyToAll,
                bytes = new byte[Encoder.TickMessageLength]
            };
            var tickUdpMessage = new OutgoingMessage
            {
                tick = LocalTick,
                caller = localId,
                callee = ClientId.None,
                rpcId = new RpcId(RpcId.NextTickId),
                target = NetworkId.None,
                protocol = Protocol.Udp,
                perms = RpcPerms.AnyToAll,
                bytes = new byte[Encoder.TickMessageLength]
            };
            var timestamp = Timestamp.Now;
            Encoder.EncodeNextTick(tickTcpMessage.bytes, localId, ClientId.None, LocalTick, timestamp);
            Encoder.EncodeNextTick(tickUdpMessage.bytes, localId, ClientId.None, LocalTick, timestamp);
            q.Enqueue(tickTcpMessage, tickTcpMessage.tick);
            q.Enqueue(tickUdpMessage, tickUdpMessage.tick);

            if (Logger.includes.simulationEncodings)
                Logger.Write("SENDING:\n" + TickEncodingSummary(new RpcId(RpcId.NextTickId), localId, ClientId.None, LocalTick, timestamp));
        }

        protected void SendCurTick(SimplePriorityQueue<OutgoingMessage, uint> q, ClientId client)
        {
            var localId = ClientRegistry.GetLocalId();

            var outgoing = new OutgoingMessage
            {
                caller = localId,
                callee = client,
                rpcId = new RpcId(RpcId.CurTickId),
                tick = LocalTick,
                protocol = Protocol.Tcp,
                perms = RpcPerms.AuthorityToClients,
                bytes = new byte[Encoder.TickMessageLength]
            };
            var timestamp = Timestamp.Now;
            Encoder.EncodeCurTick(outgoing.bytes, localId, client, LocalTick, timestamp);
            q.Enqueue(outgoing, LocalTick);
                
            if (Logger.includes.rpcCallEncodings)
                Logger.Write("SENDING:\n" + TickEncodingSummary(new RpcId(RpcId.CurTickId), localId, client, LocalTick, timestamp));
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