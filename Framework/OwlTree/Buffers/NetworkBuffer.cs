using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace OwlTree
{
    /// <summary>
    /// Use to label which protocol that will be used to send the message.
    /// </summary>
    public enum Protocol
    {
        Tcp,
        Udp
    }

    /// <summary>
    /// Super class that declares the interface for client and server buffers.
    /// </summary>
    public abstract class NetworkBuffer
    {
        /// <summary>
        /// Function signature used to decode raw byte arrays into Message structs.
        /// </summary>
        public delegate bool Decoder(ClientId caller, ReadOnlySpan<byte> bytes, out Message message);

        /// <summary>
        /// Function signature used to encode a Message struct into raw bytes.
        /// </summary>
        public delegate void Encoder(Message message, Packet buffer);

        /// <summary>
        /// Describes an RPC call, and its relevant meta data.
        /// </summary>
        public struct Message
        {
            /// <summary>
            /// Who sent the message. A caller of ClientId.None means it came from the server.
            /// </summary>
            public ClientId caller;

            /// <summary>
            /// Who should receive the message. A callee of ClientId.None means is should be sent to all sockets.
            /// </summary>
            public ClientId callee;

            /// <summary>
            /// The RPC this message is passing the arguments for.
            /// </summary>
            public RpcId rpcId;

            /// <summary>
            /// The NetworkId of the object that sent this message.
            /// </summary>
            public NetworkId target;

            /// <summary>
            /// Which protocol that will be used to send the message.
            /// </summary>
            public Protocol protocol;

            /// <summary>
            /// The arguments of the RPC call this message represents.
            /// </summary>
            public object[] args;

            /// <summary>
            /// The byte encoding of the message.
            /// </summary>
            public byte[] bytes;

            /// <summary>
            /// Describes an RPC call, and its relevant meta data.
            /// </summary>
            public Message(ClientId caller, ClientId callee, RpcId rpcId, NetworkId target, Protocol protocol, object[] args)
            {
                this.caller = caller;
                this.callee = callee;
                this.rpcId = rpcId;
                this.target = target;
                this.protocol = protocol;
                this.args = args;
                bytes = null;
            }

            public Message(ClientId callee, RpcId rpcId, object[] args)
            {
                this.caller = ClientId.None;
                this.callee = callee;
                this.rpcId = rpcId;
                this.target = NetworkId.None;
                this.protocol = Protocol.Tcp;
                this.args = args;
                bytes = null;
            }

            /// <summary>
            /// Represents an empty message.
            /// </summary>
            public static Message Empty = new Message(ClientId.None, ClientId.None, RpcId.None, NetworkId.None, Protocol.Tcp, null);

            /// <summary>
            /// Returns true if this message doesn't contain anything.
            /// </summary>
            public bool IsEmpty { get { return args == null; } }
        }

        public struct Args
        {
            public ushort owlTreeVer;
            public ushort minOwlTreeVer;
            public ushort appVer;
            public ushort minAppVer;
            public string appId;

            public string addr;
            public int tcpPort;
            public int serverUdpPort;
            public int clientUdpPort;

            public int bufferSize;

            public Decoder decoder;
            public Encoder encoder;

            public Logger logger;
        }

        /// <summary>
        /// The size of read and write buffers in bytes.
        /// Exceeding this size will result in lost data.
        /// </summary>
        public int BufferSize { get; private set; }

        protected byte[] ReadBuffer;
        protected Packet ReadPacket;

        // ip and port number this client is bound to
        public int TcpPort { get; private set; } 
        public int ServerUdpPort { get; private set; }
        public int ClientUdpPort { get; private set; }
        public IPAddress Address { get; private set; }

        public ushort OwlTreeVersion { get; private set; }
        public ushort MinOwlTreeVersion { get; private set; }
        public ushort AppVersion { get; private set; }
        public ushort MinAppVersion { get; private set; }
        public AppId ApplicationId { get; private set; }

        protected Logger Logger { get; private set; }

        public NetworkBuffer(Args args)
        {
            if (args.owlTreeVer < args.minOwlTreeVer)
            {
                throw new ArgumentException("The local connection instance is using an older version of Owl Tree than the minimum requirement.");
            }

            if (args.appVer < args.minAppVer)
            {
                throw new ArgumentException("The local connection instance is using an older app version than the minimum requirement.");
            }

            OwlTreeVersion = args.owlTreeVer;
            MinOwlTreeVersion = args.minOwlTreeVer;
            AppVersion = args.appVer;
            MinAppVersion = args.minAppVer;
            ApplicationId = new AppId(args.appId);

            Address = IPAddress.Parse(args.addr);
            TcpPort = args.tcpPort;
            ServerUdpPort = args.serverUdpPort;
            ClientUdpPort = args.clientUdpPort;
            BufferSize = args.bufferSize;
            ReadBuffer = new byte[BufferSize];
            TryDecode = args.decoder;
            Encode = args.encoder;
            ReadPacket = new Packet(BufferSize);

            Logger = args.logger;
        }

        protected void PacketToString(Packet p, StringBuilder str)
        {
            var packet = p.GetPacket();
            for (int i = 0; i < packet.Length; i++)
            {
                str.Append(packet[i].ToString("X2"));
                if (i < packet.Length - 1)
                    str.Append('-');
                if (i % 32 == 0 && i != 0)
                    str.Append('\n');
            }
        }

        /// <summary>
        /// Invoked when a new client connects.
        /// </summary>
        public ClientId.Delegate OnClientConnected;

        /// <summary>
        /// Invoked when a client disconnects.
        /// </summary>
        public ClientId.Delegate OnClientDisconnected;

        /// <summary>
        /// Invoked when the local connection is ready. Provides the local ClientId.
        /// If this is a server instance, then the ClientId will be <c>ClientId.None</c>.
        /// </summary>
        public ClientId.Delegate OnReady;

        /// <summary>
        /// Injected decoding scheme for messages.
        /// </summary>
        protected Decoder TryDecode;

        /// <summary>
        /// Injected encoding scheme for messages.
        /// </summary>
        protected Encoder Encode;

        /// <summary>
        /// Whether or not the connection is ready. 
        /// For clients, this means the server has assigned it a ClientId.
        /// </summary>
        public bool IsReady { get; protected set; } = false;

        /// <summary>
        /// The client id for the local instance. A server's local id will be <c>ClientId.None</c>
        /// </summary>
        public ClientId LocalId { get; protected set; }

        // currently read messages
        protected ConcurrentQueue<Message> _incoming = new ConcurrentQueue<Message>();

        protected ConcurrentQueue<Message> _outgoing = new ConcurrentQueue<Message>();

        /// <summary>
        /// True if there are messages that are waiting to be sent.
        /// </summary>
        public bool HasOutgoing { get { return _outgoing.Count > 0; } }
        
        /// <summary>
        /// Get the next message in the read queue.
        /// </summary>
        /// <param name="message">The next message.</param>
        /// <returns>True if there is a message, false if the queue is empty.</returns>
        public bool GetNextMessage(out Message message)
        {
            if (_incoming.Count == 0)
            {
                message = Message.Empty;
                return false;
            }
            return _incoming.TryDequeue(out message);
        }

        /// <summary>
        /// Reads any data currently on sockets. Putting new messages in the queue, and connecting new clients.
        /// </summary>
        public abstract void Read();

        /// <summary>
        /// Add message to outgoing message queue.
        /// Actually send buffers to sockets with <c>Send()</c>.
        /// </summary>
        public void AddMessage(Message message)
        {
            _outgoing.Enqueue(message);
        }

        /// <summary>
        /// Send current buffers to associated sockets.
        /// Buffers are cleared after writing.
        /// </summary>
        public abstract void Send();
        
        /// <summary>
        /// Function signature for transformer steps. Should return the same span of bytes
        /// provided as an argument.
        /// </summary>
        /// <returns></returns>
        public delegate void BufferAction(Packet packet);

        /// <summary>
        /// Use to add transformer steps to sending and reading.
        /// Specify the priority to sort the order of transformers.
        /// Sorted in ascending order.
        /// </summary>
        public struct Transformer
        {
            public int priority;
            public BufferAction step;
        }

        // buffer transformer steps
        private List<Transformer> _sendProcess = new List<Transformer>();
        private List<Transformer> _readProcess = new List<Transformer>();

        /// <summary>
        /// Adds the given transformer step to the send process.
        /// The provided BufferAction will be applied to all buffers sent.
        /// </summary>
        public void AddSendStep(Transformer step)
        {
            for (int i = 0; i < _sendProcess.Count; i++)
            {
                if (_sendProcess[i].priority > step.priority)
                {
                    _sendProcess.Insert(i, step);
                    return;
                }
            }
            _sendProcess.Add(step);
        }

        /// <summary>
        /// Apply all of the currently added send transformer steps. Returns the 
        /// same span, with transformations applied to the underlying bytes.
        /// </summary>
        protected void ApplySendSteps(Packet packet)
        {
            foreach (var step in _sendProcess)
            {
                step.step(packet);
            }
        }

        /// <summary>
        /// Adds the given transformer step to the read process.
        /// The provided BufferAction will be applied to all buffers that are received.
        /// </summary>
        public void AddReadStep(Transformer step)
        {
            for (int i = 0; i < _readProcess.Count; i++)
            {
                if (_readProcess[i].priority > step.priority)
                {
                    _readProcess.Insert(i, step);
                    return;
                }
            }
            _readProcess.Add(step);
        }
        
        /// <summary>
        /// Apply all of the currently added read transformer steps. Returns the 
        /// same span, with transformations applied to the underlying bytes.
        /// </summary>
        protected void ApplyReadSteps(Packet packet)
        {
            foreach (var step in _readProcess)
            {
                step.step(packet);
            }
        }

        /// <summary>
        /// Close the local connection.
        /// Invokes <c>OnClientDisconnected</c> with the local ClientId.
        /// </summary>
        public abstract void Disconnect();
        
        /// <summary>
        /// Disconnect a client from the server.
        /// Invokes <c>OnClientDisconnected</c>.
        /// </summary>
        public abstract void Disconnect(ClientId id);

        // * Connection and Disconnection Message Protocols

        /// <summary>
        /// The number of bytes required to encode client events.
        /// </summary>
        protected static int ClientMessageLength { get { return RpcId.MaxLength() + ClientId.MaxLength(); } }

        /// <summary>
        /// The number of bytes required to encode the local client connected event.
        /// </summary>
        protected static int LocalClientConnectLength { get { return RpcId.MaxLength() + ClientId.MaxLength() + 4; } }

        /// <summary>
        /// The number of bytes required to encode a new connection request.
        /// </summary>
        protected static int ConnectionRequestLength { get { return RpcId.MaxLength() + AppId.MaxLength(); } }

        protected static void ClientConnectEncode(Span<byte> bytes, ClientId id)
        {
            var rpcId = new RpcId(RpcId.CLIENT_CONNECTED_MESSAGE_ID);
            var ind = rpcId.ByteLength();
            rpcId.InsertBytes(bytes.Slice(0, ind));
            id.InsertBytes(bytes.Slice(ind, id.ByteLength()));
        }

        protected static void LocalClientConnectEncode(Span<byte> bytes, ClientId id, UInt32 hash)
        {
            var rpcId = new RpcId(RpcId.LOCAL_CLIENT_CONNECTED_MESSAGE_ID);
            var ind = rpcId.ByteLength();
            rpcId.InsertBytes(bytes.Slice(0, ind));
            id.InsertBytes(bytes.Slice(ind, id.ByteLength()));
            ind += id.ByteLength();
            BitConverter.TryWriteBytes(bytes.Slice(ind), hash);
        }

        protected static void ClientDisconnectEncode(Span<byte> bytes, ClientId id)
        {
            var rpcId = new RpcId(RpcId.CLIENT_DISCONNECTED_MESSAGE_ID);
            var ind = rpcId.ByteLength();
            rpcId.InsertBytes(bytes.Slice(0, ind));
            id.InsertBytes(bytes.Slice(ind, id.ByteLength()));
        }

        protected static void ConnectionRequestEncode(Span<byte> bytes, AppId id)
        {
            var rpc = new RpcId(RpcId.CONNECTION_REQUEST);
            var ind = rpc.ByteLength();
            rpc.InsertBytes(bytes);
            id.InsertBytes(bytes.Slice(ind));
        }

        protected static RpcId ServerMessageDecode(ReadOnlySpan<byte> bytes, out AppId id)
        {
            RpcId result = RpcId.None;
            result.FromBytes(bytes);
            id = new AppId();
            switch (result.Id)
            {
                case RpcId.CONNECTION_REQUEST:
                    id.FromBytes(bytes.Slice(result.ByteLength()));
                    break;
            }
            return result;
        }

        protected static RpcId ClientMessageDecode(ReadOnlySpan<byte> message, out ClientId id, out UInt32 hash)
        {
            RpcId result = RpcId.None;
            hash = 0;
            UInt32 rpcId = BitConverter.ToUInt32(message);
            switch(rpcId)
            {
                case RpcId.CLIENT_CONNECTED_MESSAGE_ID:
                    result = new RpcId(RpcId.CLIENT_CONNECTED_MESSAGE_ID);
                    break;
                case RpcId.LOCAL_CLIENT_CONNECTED_MESSAGE_ID:
                    result = new RpcId(RpcId.LOCAL_CLIENT_CONNECTED_MESSAGE_ID);
                    hash = BitConverter.ToUInt32(message.Slice(result.ByteLength() + ClientId.MaxLength()));
                    break;
                case RpcId.CLIENT_DISCONNECTED_MESSAGE_ID:
                    result = new RpcId(RpcId.CLIENT_DISCONNECTED_MESSAGE_ID);
                    break;
                default:
                    id = ClientId.None;
                    hash = 0;
                    return RpcId.None;
            }
            id = new ClientId(message.Slice(result.ByteLength()));
            return result;
        }
    }
}