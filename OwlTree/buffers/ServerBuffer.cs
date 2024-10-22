
using System;
using System.Collections;
using System.Collections.Generic;
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
        /// <param name="tpcPort">The port to bind the TCP socket to.</param>
        /// /// <param name="serverUdpPort">The port to bind the UDP socket to.</param>
        /// <param name="maxClients">The max number of clients that can be connected at once.</param>
        /// <param name="bufferSize">The size of read and write buffers in bytes.</param>
        public ServerBuffer(string addr, int tpcPort, int serverUdpPort, int clientUdpPort, byte maxClients, int bufferSize, Decoder decoder, Encoder encoder) : base (addr, tpcPort, serverUdpPort, clientUdpPort, bufferSize, decoder, encoder)
        {

            IPEndPoint tpcEndPoint = new IPEndPoint(IPAddress.Any, TcpPort);
            _tcpServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _tcpServer.Bind(tpcEndPoint);
            _tcpServer.Listen(maxClients);
            _readList.Add(_tcpServer);

            IPEndPoint udpEndPoint = new IPEndPoint(IPAddress.Any, ServerUdpPort);
            _udpServer = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _udpServer.Bind(udpEndPoint);
            _readList.Add(_udpServer);

            MaxClients = maxClients;
            LocalId = ClientId.None;
            IsReady = true;
            OnReady?.Invoke(LocalId);
        }

        /// <summary>
        /// The maximum number of clients allowed to be connected at once on this connection.
        /// </summary>
        public int MaxClients { get; private set; }

        private Random _rand = new Random();

        // used to map a client's socket to its id and buffer
        private struct ClientData
        {
            public ClientId id;
            public UInt32 hash;
            public MessageBuffer tcpBuffer;
            public Socket tpcSocket;
            public MessageBuffer udpBuffer;
            public IPEndPoint udpEndPoint;

            public static ClientData None = new ClientData() { id = ClientId.None };

            public static bool operator ==(ClientData a, ClientData b) => a.id == b.id;
            public static bool operator !=(ClientData a, ClientData b) => a.id != b.id;

            public override bool Equals(object obj)
            {
                return obj != null && obj.GetType() == typeof(ClientData) && ((ClientData)obj == this);
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
        }

        // server state
        private Socket _tcpServer;
        private Socket _udpServer;
        private List<Socket> _readList = new List<Socket>();
        private List<ClientData> _clientData = new List<ClientData>();

        private ClientData FindClientData(Socket s)
        {
            foreach (var data in _clientData)
                if (data.tpcSocket == s) return data;
            return ClientData.None;
        }

        private ClientData FindClientData(ClientId id)
        {
            foreach (var data in _clientData)
                if (data.id == id) return data;
            return ClientData.None;
        }

        private ClientData FindClientData(UInt32 hash)
        {
            foreach (var data in _clientData)
                if (data.hash == hash) return data;
            return ClientData.None;
        }

        private ClientId[] GetClientIds()
        {
            ClientId[] ids = new ClientId[_clientData.Count];
            for (int i = 0; i < _clientData.Count; i++)
                ids[i] = _clientData[i].id;
            return ids;
        }

        /// <summary>
        /// Reads any data currently on sockets. Putting new messages in the queue, and connecting new clients.
        /// </summary>
        public override void Read()
        {
            _readList.Clear();
            _readList.Add(_tcpServer);
            _readList.Add(_udpServer);
            foreach (var data in _clientData)
                _readList.Add(data.tpcSocket);
            
            Socket.Select(_readList, null, null, 0);

            foreach (var socket in _readList)
            {
                // new client connects
                if (socket == _tcpServer)
                {
                    var tcpClient = socket.Accept();

                    IPEndPoint udpEndPoint = new IPEndPoint(((IPEndPoint)tcpClient.RemoteEndPoint).Address, ClientUdpPort);
                    var hash = (UInt32)_rand.Next();

                    var clientData = new ClientData() {
                        id = ClientId.New(), 
                        hash = hash, 
                        tcpBuffer = new MessageBuffer(BufferSize), 
                        tpcSocket = tcpClient,
                        udpBuffer = new MessageBuffer(BufferSize),
                        udpEndPoint = udpEndPoint
                    };

                    _clientData.Add(clientData);

                    OnClientConnected?.Invoke(clientData.id);

                    // send new client their id
                    var span = clientData.tcpBuffer.GetSpan(LocalClientConnectLength);
                    LocalClientConnectEncode(span, clientData.id, clientData.hash);

                    foreach (var otherClient in _clientData)
                    {
                        if (otherClient.id == clientData.id) continue;

                        // notify clients of a new client in the next send
                        span = otherClient.tcpBuffer.GetSpan(ClientMessageLength);
                        ClientConnectEncode(span, clientData.id);

                        // add existing clients to new client
                        span = clientData.tcpBuffer.GetSpan(ClientMessageLength);
                        ClientConnectEncode(span, otherClient.id);
                    }
                    
                    var bytes = clientData.tcpBuffer.GetBuffer();
                    bytes = ApplySendSteps(bytes);
                    tcpClient.Send(bytes);
                    clientData.tcpBuffer.Reset();
                }
                else if (socket == _udpServer) // receive client udp messages
                {
                    Array.Clear(ReadBuffer, 0, ReadBuffer.Length);

                    EndPoint source = new IPEndPoint(IPAddress.Any, 0);
                    int dataLen = -1;
                    try
                    {
                        dataLen = socket.ReceiveFrom(ReadBuffer, ref source);
                    }
                    catch { }

                    UInt32 hash = BitConverter.ToUInt32(ReadBuffer);

                    var transformed = ApplyReadSteps(ReadBuffer.AsSpan(4));

                    var client = FindClientData(hash);

                    int start = 0;
                    while (MessageBuffer.GetNextMessage(transformed, ref start, out var bytes))
                    {
                        if (TryDecode(client.id, bytes, out var message))
                        {
                            _incoming.Enqueue(message);
                        }
                    }
                }
                else // receive client tcp messages
                {
                    Array.Clear(ReadBuffer, 0, ReadBuffer.Length);

                    int dataLen = -1;
                    try
                    {
                        dataLen = socket.Receive(ReadBuffer);
                    }
                    catch { }

                    var transformed = ApplyReadSteps(ReadBuffer.AsSpan());

                    var client = FindClientData(socket);

                    // disconnect if receive fails
                    if (dataLen <= 0)
                    {
                        _clientData.Remove(client);
                        socket.Close();
                        OnClientDisconnected?.Invoke(client.id);

                        foreach (var otherClient in _clientData)
                        {
                            var span = otherClient.tcpBuffer.GetSpan(ClientMessageLength);
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
                    var client = FindClientData(message.callee);
                    if (client != ClientData.None)
                    {
                        if (message.protocol == Protocol.Tcp)
                            Encode(message, client.tcpBuffer);
                        else
                            Encode(message, client.udpBuffer);
                    }
                }
                else
                {
                    if (message.protocol == Protocol.Tcp)
                    {
                        foreach (var client in _clientData)
                        {
                            Encode(message, client.tcpBuffer);
                        }
                    }
                    else
                    {
                        foreach (var client in _clientData)
                        {
                            Encode(message, client.udpBuffer);
                        }
                    }
                }
            }
            foreach (var client in _clientData)
            {
                if (!client.tcpBuffer.IsEmpty)
                {
                    var bytes = client.tcpBuffer.GetBuffer();
                    bytes = ApplySendSteps(bytes);
                    client.tpcSocket.Send(bytes);
                    client.tcpBuffer.Reset();
                }

                if (!client.udpBuffer.IsEmpty)
                {
                    var bytes = client.udpBuffer.GetBuffer();
                    bytes = ApplySendSteps(bytes);
                    _udpServer.SendTo(bytes.ToArray(), client.udpEndPoint);
                    client.udpBuffer.Reset();
                }
            }
        }

        /// <summary>
        /// Disconnects all clients, and closes the server.
        /// </summary>
        public override void Disconnect()
        {
            var ids = GetClientIds();
            foreach (var id in ids)
            {
                Disconnect(id);
            }
            _tcpServer.Close();
            _udpServer.Close();
        }

        /// <summary>
        /// Disconnect a client from the server.
        /// Invokes <c>OnClientDisconnected</c>.
        /// </summary>
        public override void Disconnect(ClientId id)
        {
            var client = FindClientData(id);
            if (client != ClientData.None)
            {
                _clientData.Remove(client);
                client.tpcSocket.Close();
                OnClientDisconnected?.Invoke(id);

                foreach (var otherClient in _clientData)
                {
                    var span = otherClient.tcpBuffer.GetSpan(ClientMessageLength);
                    ClientDisconnectEncode(span, client.id);
                }
            }
        }
    }
}