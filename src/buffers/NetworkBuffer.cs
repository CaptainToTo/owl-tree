using System.Collections.Concurrent;
using System.Net;

namespace OwlTree
{
    /// <summary>
    /// Super class that declares the interface for client and server buffers.
    /// </summary>
    public abstract class NetworkBuffer
    {

        public delegate bool Decoder(ClientId caller, ReadOnlySpan<byte> message, out RpcId rpcId, out NetworkId target, out object[]? args);

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
            /// The arguments of the RPC call this message represents.
            /// </summary>
            public object[]? args;

            /// <summary>
            /// Describes an RPC call, and its relevant meta data.
            /// </summary>
            public Message(ClientId caller, ClientId callee, RpcId rpcId, NetworkId target, object[]? args)
            {
                this.caller = caller;
                this.callee = callee;
                this.rpcId = rpcId;
                this.target = target;
                this.args = args;
            }

            public Message(ClientId callee, RpcId rpcId, object[]? args)
            {
                this.caller = ClientId.None;
                this.callee = callee;
                this.rpcId = rpcId;
                this.target = NetworkId.None;
                this.args = args;
            }

            /// <summary>
            /// Represents an empty message.
            /// </summary>
            public static Message Empty = new Message(ClientId.None, ClientId.None, RpcId.None, NetworkId.None, null);

            /// <summary>
            /// Returns true if this message doesn't contain anything.
            /// </summary>
            public bool IsEmpty { get { return args == null; } }
        }

        /// <summary>
        /// The size of read and write buffers in bytes.
        /// Exceeding this size will result in lost data.
        /// </summary>
        public int BufferSize { get; private set; }

        // ip and port number this client is bound to
        public int Port { get; private set; } 
        public IPAddress Address { get; private set; }

        public NetworkBuffer(string addr, int port, int bufferSize, Decoder decoder)
        {
            Address = IPAddress.Parse(addr);
            Port = port;
            BufferSize = bufferSize;
            TryDecode = decoder;
        }

        /// <summary>
        /// Invoked when a new client connects.
        /// </summary>
        public ClientId.Delegate? OnClientConnected;

        /// <summary>
        /// Invoked when a client disconnects.
        /// </summary>
        public ClientId.Delegate? OnClientDisconnected;

        /// <summary>
        /// Invoked when the local connection is ready. Provides the local ClientId.
        /// If this is a server instance, then the ClientId will be <c>ClientId.None</c>.
        /// </summary>
        public ClientId.Delegate? OnReady;

        public Decoder TryDecode;

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

        public delegate void BufferAction(Span<byte> bytes);

        public struct BufferTransformStep
        {
            public int priority;
            public BufferAction step;
        }

        private List<BufferTransformStep> _sendProcess = new List<BufferTransformStep>();
        private List<BufferTransformStep> _readProcess = new List<BufferTransformStep>();

        public void AddSendStep(BufferTransformStep step)
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

        protected void ApplySendSteps(Span<byte> bytes)
        {
            foreach (var step in _sendProcess)
            {
                step.step(bytes);
            }
        }

        public void AddReadStep(BufferTransformStep step)
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

        protected void ApplyReadSteps(Span<byte> bytes)
        {
            foreach (var step in _readProcess)
            {
                step.step(bytes);
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

        protected static int ClientMessageLength { get { return RpcId.MaxLength() + ClientId.MaxLength(); } }

        protected static void ClientConnectEncode(Span<byte> bytes, ClientId id)
        {
            var rpcId = new RpcId(RpcId.CLIENT_CONNECTED_MESSAGE_ID);
            var ind = rpcId.ExpectedLength();
            rpcId.InsertBytes(bytes.Slice(0, ind));
            id.InsertBytes(bytes.Slice(ind, id.ExpectedLength()));
        }

        protected static void LocalClientConnectEncode(Span<byte> bytes, ClientId id)
        {
            var rpcId = new RpcId(RpcId.LOCAL_CLIENT_CONNECTED_MESSAGE_ID);
            var ind = rpcId.ExpectedLength();
            rpcId.InsertBytes(bytes.Slice(0, ind));
            id.InsertBytes(bytes.Slice(ind, id.ExpectedLength()));
        }

        protected static void ClientDisconnectEncode(Span<byte> bytes, ClientId id)
        {
            var rpcId = new RpcId(RpcId.CLIENT_DISCONNECTED_MESSAGE_ID);
            var ind = rpcId.ExpectedLength();
            rpcId.InsertBytes(bytes.Slice(0, ind));
            id.InsertBytes(bytes.Slice(ind, id.ExpectedLength()));
        }

        protected static RpcId ClientMessageDecode(ReadOnlySpan<byte> message, out ClientId id)
        {
            RpcId result = RpcId.None;
            UInt16 rpcId = BitConverter.ToUInt16(message);
            switch(rpcId)
            {
                case RpcId.CLIENT_CONNECTED_MESSAGE_ID:
                    result = new RpcId(RpcId.CLIENT_CONNECTED_MESSAGE_ID);
                    break;
                case RpcId.LOCAL_CLIENT_CONNECTED_MESSAGE_ID:
                    result = new RpcId(RpcId.LOCAL_CLIENT_CONNECTED_MESSAGE_ID);
                    break;
                case RpcId.CLIENT_DISCONNECTED_MESSAGE_ID:
                    result = new RpcId(RpcId.CLIENT_DISCONNECTED_MESSAGE_ID);
                    break;
                default:
                    id = ClientId.None;
                    return RpcId.None;
            }
            id = new ClientId(message.Slice(result.ExpectedLength()));
            return result;
        }
    }
}