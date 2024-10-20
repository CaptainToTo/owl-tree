
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
        public ServerBuffer(string addr, int port, byte maxClients, int bufferSize, Decoder decoder, Encoder encoder) : base (addr, port, bufferSize, decoder, encoder)
        {

            _server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint endPoint = new IPEndPoint(Address, Port);
            _server.Bind(endPoint);
            _server.Listen(maxClients);
            _readList.Add(_server);
            MaxClients = maxClients;
            LocalId = ClientId.None;
            IsReady = true;
            OnReady?.Invoke(LocalId);
        }

        /// <summary>
        /// The maximum number of clients allowed to be connected at once on this connection.
        /// </summary>
        public int MaxClients { get; private set; }

        // used to map a client's socket to its id and buffer
        private class ClientData
        {
            public ClientId id;
            public MessageBuffer buffer;
            public Socket socket;

            public ClientData(ClientId id, MessageBuffer buffer, Socket socket)
            {
                this.id = id;
                this.buffer = buffer;
                this.socket = socket;
            }
        }

        // server state
        private Socket _server;
        private List<Socket> _readList = new List<Socket>();
        private Dictionary<Socket, ClientData> _clientsSockets = new Dictionary<Socket, ClientData>();
        private Dictionary<ClientId, ClientData> _clientsIds = new Dictionary<ClientId, ClientData>();

        /// <summary>
        /// Reads any data currently on sockets. Putting new messages in the queue, and connecting new clients.
        /// </summary>
        public override void Read()
        {
            _readList.Clear();
            _readList.Add(_server);
            foreach (var client in _clientsSockets)
                _readList.Add(client.Key);
            
            Socket.Select(_readList, null, null, 0);

            foreach (var socket in _readList)
            {
                // new client connects
                if (socket == _server)
                {
                    var client = socket.Accept();

                    var clientInstance = new ClientData(new ClientId(), new MessageBuffer(BufferSize), client);

                    _clientsSockets.Add(client, clientInstance);
                    _clientsIds.Add(clientInstance.id, clientInstance);

                    OnClientConnected?.Invoke(_clientsSockets[client].id);

                    // send new client their id
                    var span = clientInstance.buffer.GetSpan(ClientMessageLength);
                    LocalClientConnectEncode(span, clientInstance.id);

                    foreach (var otherClient in _clientsIds)
                    {
                        if (otherClient.Key == clientInstance.id) continue;

                        // notify clients of a new client in the next send
                        span = otherClient.Value.buffer.GetSpan(ClientMessageLength);
                        ClientConnectEncode(span, clientInstance.id);

                        // add existing clients to new client
                        span = clientInstance.buffer.GetSpan(ClientMessageLength);
                        ClientConnectEncode(span, otherClient.Key);
                    }
                    
                    var bytes = clientInstance.buffer.GetBuffer();
                    bytes = ApplySendSteps(bytes);
                    client.Send(bytes);
                    clientInstance.buffer.Reset();
                }
                else // receive client messages
                {
                    Array.Clear(ReadBuffer);

                    int dataLen = -1;
                    try
                    {
                        dataLen = socket.Receive(ReadBuffer);
                    }
                    catch { }

                    var transformed = ApplyReadSteps(ReadBuffer);

                    var client = _clientsSockets[socket];

                    // disconnect if receive fails
                    if (dataLen <= 0)
                    {
                        _clientsSockets.Remove(socket);
                        _clientsIds.Remove(client.id);
                        socket.Close();
                        OnClientDisconnected?.Invoke(client.id);

                        foreach (var otherClient in _clientsSockets)
                        {
                            var span = otherClient.Value.buffer.GetSpan(ClientMessageLength);
                            ClientDisconnectEncode(span, client.id);
                        }
                        continue;
                    }

                    int start = 0;
                    while (MessageBuffer.GetNextMessage(transformed, ref start, out var bytes))
                    {
                        if (TryDecode(client.id, bytes, out var message))
                        {
                            _incoming.Enqueue(message);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Write current buffers to client sockets.
        /// Buffers are cleared after writing.
        /// </summary>
        public override void Send()
        {
            while (_outgoing.TryDequeue(out var message))
            {

                if (message.callee != ClientId.None)
                {
                    if (_clientsIds.TryGetValue(message.callee, out var client))
                    {
                        Encode(message, client.buffer);
                    }
                }
                else
                {
                    foreach (var client in _clientsSockets)
                    {
                        Encode(message, client.Value.buffer);
                    }
                }
            }
            foreach (var client in _clientsSockets)
            {
                var bytes = client.Value.buffer.GetBuffer();
                bytes = ApplySendSteps(bytes);
                client.Key.Send(bytes);
                client.Value.buffer.Reset();
            }
        }

        /// <summary>
        /// Disconnects all clients, and closes the server.
        /// </summary>
        public override void Disconnect()
        {
            var ids = _clientsIds.Keys;
            foreach (var id in ids)
            {
                Disconnect(id);
            }
            _server.Close();
        }

        /// <summary>
        /// Disconnect a client from the server.
        /// Invokes <c>OnClientDisconnected</c>.
        /// </summary>
        public override void Disconnect(ClientId id)
        {
            if (_clientsIds.TryGetValue(id, out var client))
            {
                _clientsSockets.Remove(client.socket);
                _clientsIds.Remove(id);
                client.socket.Close();
                OnClientDisconnected?.Invoke(id);

                foreach (var otherClient in _clientsSockets)
                {
                    var span = otherClient.Value.buffer.GetSpan(ClientMessageLength);
                    ClientDisconnectEncode(span, client.id);
                }
            }
        }
    }
}