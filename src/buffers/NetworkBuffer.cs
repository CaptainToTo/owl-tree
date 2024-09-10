

using System.Net;

namespace OwlTree
{
    /// <summary>
    /// Super class that declares the interface for client and server buffers.
    /// </summary>
    public abstract class NetworkBuffer
    {

        /// <summary>
        /// Contains raw message bytes, and meta data about its source.
        /// </summary>
        public struct Message
        {
            /// <summary>
            /// Who sent the message. A source of ClientId.None means it came from the server.
            /// </summary>
            public ClientId source;
            /// <summary>
            /// The raw message bytes. This may be null to indicate an empty/non-existent message.
            /// </summary>
            public byte[]? bytes;

            /// <summary>
            /// Contains raw message bytes, and meta data about its source.
            /// </summary>
            public Message(ClientId source, byte[]? bytes)
            {
                this.source = source;
                this.bytes = bytes;
            }

            /// <summary>
            /// Represents an empty message.
            /// </summary>
            public static Message Empty = new Message(ClientId.None, null);

            /// <summary>
            /// Returns true if this message doesn't contain anything.
            /// </summary>
            public bool IsEmpty { get { return bytes == null || bytes.Length == 0; } }
        }

        /// <summary>
        /// The size of read and write buffers in bytes.
        /// Exceeding this size will result in lost data.
        /// </summary>
        public int BufferSize { get; private set; }

        // ip and port number this client is bound to
        public int Port { get; private set; } 
        public IPAddress Address { get; private set; }

        public NetworkBuffer(string addr, int port, int bufferSize)
        {
            Address = IPAddress.Parse(addr);
            Port = port;
            BufferSize = bufferSize;
        }

        /// <summary>
        /// Invoked when a new client connects.
        /// </summary>
        public Action<ClientId>? OnClientConnected;

        /// <summary>
        /// Invoked when a client disconnects.
        /// </summary>
        public Action<ClientId>? OnClientDisconnected;

        /// <summary>
        /// Invoked when the local connection is ready. Provides the local ClientId.
        /// If this is a server instance, then the ClientId will be <c>ClientId.None</c>.
        /// </summary>
        public Action<ClientId>? OnReady;

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
        protected Queue<Message> _incoming = new Queue<Message>();

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
            message = _incoming.Dequeue();
            return true;
        }

        /// <summary>
        /// Reads any data currently on sockets. Putting new messages in the queue, and connecting new clients.
        /// </summary>
        public abstract void Read();

        /// <summary>
        /// Add message to all available buffers.
        /// Actually send buffers to sockets with <c>Send()</c>.
        /// </summary>
        public abstract void Write(byte[] message);

        /// <summary>
        /// Add message to a specific client's buffer.
        /// Actually send buffers to sockets with <c>Send()</c>.
        /// </summary>
        public abstract void WriteTo(ClientId id, byte[] message);

        /// <summary>
        /// Send current buffers to associated sockets.
        /// Buffers are cleared after writing.
        /// </summary>
        public abstract void Send();

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

        protected const byte CLIENT_CONNECTED_MESSAGE_ID = 0;
        protected const byte LOCAL_CLIENT_CONNECTED_MESSAGE_ID = 1;
        protected const byte CLIENT_DISCONNECTED_MESSAGE_ID = 2;

        protected static byte[] ClientConnectEncode(ClientId id)
        {
            var bytes = new byte[]{CLIENT_CONNECTED_MESSAGE_ID, 0, 0, 0, 0};
            id.InsertBytes(ref bytes, 1);
            return bytes;
        }

        protected static byte[] LocalClientConnectEncode(ClientId id)
        {
            var bytes = new byte[]{LOCAL_CLIENT_CONNECTED_MESSAGE_ID, 0, 0, 0, 0};
            id.InsertBytes(ref bytes, 1);
            return bytes;
        }

        protected static byte[] ClientDisconnectEncode(ClientId id)
        {
            var bytes = new byte[]{CLIENT_DISCONNECTED_MESSAGE_ID, 0, 0, 0, 0};
            id.InsertBytes(ref bytes, 1);
            return bytes;
        }

        protected static int ClientMessageDecode(byte[] message, out ClientId id)
        {
            int result;
            switch(message[0])
            {
                case CLIENT_CONNECTED_MESSAGE_ID:
                    result = CLIENT_CONNECTED_MESSAGE_ID;
                    break;
                case LOCAL_CLIENT_CONNECTED_MESSAGE_ID:
                    result = LOCAL_CLIENT_CONNECTED_MESSAGE_ID;
                    break;
                case CLIENT_DISCONNECTED_MESSAGE_ID:
                    result = CLIENT_DISCONNECTED_MESSAGE_ID;
                    break;
                default:
                    id = ClientId.None;
                    return -1;
            }
            id = new ClientId(message, 1);
            return result;
        }
    }
}