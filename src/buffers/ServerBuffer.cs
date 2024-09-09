
using System.Collections;
using System.Net;
using System.Net.Sockets;

namespace OwlTree
{
    /// <summary>
    /// Manages sending and receiving messages for a server instance.
    /// </summary>
    public class ServerBuffer : NetworkBuffer
    {
        /// <summary>
        /// Manages sending and receiving messages for a server instance.
        /// </summary>
        /// <param name="addr">The server's IP address.</param>
        /// <param name="port">The port to bind to.</param>
        /// <param name="maxClients">The max number of clients that can be connected at once.</param>
        /// <param name="bufferSize">The size of read and write buffers in bytes. Exceeding the size of these buffers will result in lost data.</param>
        public ServerBuffer(string addr, int port, byte maxClients, int bufferSize) : base (addr, port, bufferSize)
        {

            _server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint endPoint = new IPEndPoint(Address, Port);
            _server.Bind(endPoint);
            _server.Listen(maxClients);
            readList.Add(_server);
            MaxClients = maxClients;
        }

        public int MaxClients { get; private set; }

        // used to map a client's socket to its id and buffer
        private struct ClientInstance
        {
            public ClientId id;
            public MessageBuffer buffer;
        }

        // server state
        private Socket _server;
        private List<Socket> readList = new List<Socket>();
        private Dictionary<Socket, ClientInstance> clients = new Dictionary<Socket, ClientInstance>();

        // currently read messages
        private Queue<Message> incoming = new Queue<Message>();

        /// <summary>
        /// Get the next message in the read queue.
        /// </summary>
        /// <param name="message">The next message.</param>
        /// <returns>True if there is a message, false if the queue is empty.</returns>
        public bool GetNextMessage(out Message message)
        {
            if (incoming.Count == 0)
            {
                message = Message.Empty;
                return false;
            }
            message = incoming.Dequeue();
            return true;
        }

        /// <summary>
        /// Reads any data currently on sockets. Putting new messages in the queue, and connecting new clients.
        /// </summary>
        public void Read()
        {
            Socket.Select(readList, null, null, 0);

            byte[] data = new byte[BufferSize];
            List<byte[]> messages = new List<byte[]>();

            foreach (var socket in readList)
            {
                // new client connects
                if (socket == _server)
                {
                    var client = socket.Accept();

                    readList.Add(client);
                    clients.Add(
                        client, 
                        new ClientInstance {
                            id = new ClientId(),
                            buffer = new MessageBuffer(BufferSize)
                        }
                    );

                    OnClientConnected?.Invoke(clients[client].id);
                }
                else // receive client messages
                {
                    int dataLen = -1;
                    try
                    {
                        dataLen = socket.Receive(data);
                    }
                    catch { }

                    var client = clients[socket];

                    // disconnect if receive fails
                    if (dataLen <= 0)
                    {
                        clients.Remove(socket);
                        readList.Remove(socket);
                        socket.Close();
                        OnClientDisconnected?.Invoke(client.id);
                        continue;
                    }

                    messages.Clear();
                    MessageBuffer.SplitMessageBytes(data, ref messages);
                    
                    foreach (var message in messages)
                    {
                        incoming.Enqueue(new Message(client.id, ClientId.None, message));
                    }
                }
            }
        }

        /// <summary>
        /// Add message to all clients' buffers.
        /// Actually write buffers to sockets with <c>Write()</c>.
        /// </summary>
        public void Broadcast(byte[] message)
        {
            foreach (var client in clients)
            {
                try
                {
                    client.Value.buffer.Add(message);
                }
                catch { }
            }
        }

        private Socket? GetSocket(ClientId id)
        {
            foreach (var client in clients)
            {
                if (client.Value.id == id)
                {
                    return client.Key;
                }
            }
            return null;
        }

        /// <summary>
        /// Add message to a specific client's buffer.
        /// Actually write buffers to sockets with <c>Write()</c>.
        /// </summary>
        public void SendTo(ClientId id, byte[] message)
        {
            var client = GetSocket(id);
            if (client != null)
            {
                try
                {
                    clients[client].buffer.Add(message);
                }
                catch { }
            }
        }

        /// <summary>
        /// Write current buffers to client sockets.
        /// Buffers are cleared after writing.
        /// </summary>
        public void Write()
        {
            foreach (var client in clients)
            {
                client.Key.Send(client.Value.buffer.GetBuffer());
                client.Value.buffer.Reset();
            }
        }

        /// <summary>
        /// Disconnect a client from the server.
        /// Invokes <c>OnClientDisconnected</c>.
        /// </summary>
        public void Disconnect(ClientId id)
        {
            var socket = GetSocket(id);
            if (socket != null)
            {
                clients.Remove(socket);
                readList.Remove(socket);
                socket.Close();
                OnClientDisconnected?.Invoke(id);
            }
        }
    }
}