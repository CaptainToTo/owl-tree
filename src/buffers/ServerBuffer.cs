
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
            _readList.Add(_server);
            MaxClients = maxClients;
            LocalId = ClientId.None;
            IsReady = true;
            OnReady?.Invoke(LocalId);
        }

        public int MaxClients { get; private set; }

        // used to map a client's socket to its id and buffer
        private class ClientInstance
        {
            public ClientId id;
            public MessageBuffer buffer;
            public Socket socket;

            public ClientInstance(ClientId id, MessageBuffer buffer, Socket socket)
            {
                this.id = id;
                this.buffer = buffer;
                this.socket = socket;
            }
        }

        // server state
        private Socket _server;
        private List<Socket> _readList = new List<Socket>();
        private Dictionary<Socket, ClientInstance> _clientsSockets = new Dictionary<Socket, ClientInstance>();
        private Dictionary<ClientId, ClientInstance> _clientsIds = new Dictionary<ClientId, ClientInstance>();

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

            byte[] data = new byte[BufferSize];
            List<byte[]> messages = new List<byte[]>();

            foreach (var socket in _readList)
            {
                // new client connects
                if (socket == _server)
                {
                    var client = socket.Accept();

                    var clientInstance = new ClientInstance(new ClientId(), new MessageBuffer(BufferSize), client);

                    _clientsSockets.Add(client, clientInstance);
                    _clientsIds.Add(clientInstance.id, clientInstance);

                    OnClientConnected?.Invoke(_clientsSockets[client].id);

                    // send new client their id
                    clientInstance.buffer.Add(LocalClientConnectEncode(clientInstance.id));

                    // notify clients of a new client in the next send
                    var clientConnectedMessage = ClientConnectEncode(clientInstance.id);
                    foreach (var otherClient in _clientsIds)
                    {
                        WriteTo(otherClient.Key, clientConnectedMessage);
                        clientInstance.buffer.Add(ClientConnectEncode(otherClient.Key));
                    }
                    
                    client.Send(clientInstance.buffer.GetBuffer());
                    clientInstance.buffer.Reset();
                }
                else // receive client messages
                {
                    int dataLen = -1;
                    try
                    {
                        dataLen = socket.Receive(data);
                    }
                    catch { }

                    var client = _clientsSockets[socket];

                    // disconnect if receive fails
                    if (dataLen <= 0)
                    {
                        _clientsSockets.Remove(socket);
                        _clientsIds.Remove(client.id);
                        socket.Close();
                        OnClientDisconnected?.Invoke(client.id);
                        Write(ClientDisconnectEncode(client.id));
                        continue;
                    }

                    messages.Clear();
                    MessageBuffer.SplitMessageBytes(data, ref messages);
                    
                    foreach (var message in messages)
                    {
                        var args = RpcAttribute.DecodeRpc(client.id, message, out var protocol, out var target);
                        _incoming.Enqueue(new Message(client.id, ClientId.None, protocol.Id, target, args));
                    }
                }
            }
        }

        /// <summary>
        /// Add message to all clients' buffers.
        /// Actually write buffers to sockets with <c>Write()</c>.
        /// </summary>
        protected override void Write(byte[] message)
        {
            foreach (var client in _clientsSockets)
            {
                try
                {
                    client.Value.buffer.Add(message);
                }
                catch { }
            }
        }

        /// <summary>
        /// Add message to a specific client's buffer.
        /// Actually write buffers to sockets with <c>Write()</c>.
        /// </summary>
        protected override void WriteTo(ClientId id, byte[] message)
        {
            if (_clientsIds.TryGetValue(id, out var client))
            {
                try
                {
                    client.buffer.Add(message);
                }
                catch { }
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
                if (message.rpcId == RpcId.NETWORK_OBJECT_SPAWN)
                    Write(NetworkSpawner.SpawnEncode((Type)message.args![0], (NetworkId)message.args![1]));
                else if (message.rpcId == RpcId.NETWORK_OBJECT_DESPAWN)
                    Write(NetworkSpawner.DespawnEncode((NetworkId)message.args![0]));
                else if (message.callee != ClientId.None)
                    WriteTo(message.callee, RpcAttribute.EncodeRpc(message.rpcId, message.target, message.args));
                else
                    Write(RpcAttribute.EncodeRpc(message.rpcId, message.target, message.args));
            }
            foreach (var client in _clientsSockets)
            {
                client.Key.Send(client.Value.buffer.GetBuffer());
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
                Write(ClientDisconnectEncode(client.id));
            }
        }
    }
}