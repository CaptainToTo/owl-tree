

using System.Net;

namespace OwlTree
{
    public abstract class NetworkBuffer
    {
        public struct Message
        {
            public ClientId source;
            public ClientId target;
            public byte[]? bytes;

            public Message(ClientId source, ClientId target, byte[]? bytes)
            {
                this.source = source;
                this.target = target;
                this.bytes = bytes;
            }

            public static Message Empty = new Message(ClientId.None, ClientId.None, null);

            public bool IsEmpty { get { return bytes == null || bytes.Length == 0; } }
        }

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
    }
}