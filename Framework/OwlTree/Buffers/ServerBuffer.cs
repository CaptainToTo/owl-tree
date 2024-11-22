
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

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
        /// <param name="args">NetworkBuffer parameters.</param>
        /// <param name="maxClients">The max number of clients that can be connected at once.</param>
        public ServerBuffer(Args args, byte maxClients) : base (args)
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
            public Packet tcpPacket;
            public Socket tpcSocket;
            public Packet udpPacket;
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
        private List<IPEndPoint> _connectionRequests = new List<IPEndPoint>();

        private bool GetConnectionRequest(IPEndPoint endPoint)
        {
            for (int i = 0; i < _connectionRequests.Count; i++)
            {
                if (_connectionRequests[i].Address.Equals(endPoint.Address))
                {
                    _connectionRequests.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

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

        private ClientData FindClientData(IPEndPoint endPoint)
        {
            foreach (var data in _clientData)
                if (data.udpEndPoint.Address.Equals(endPoint.Address)) return data;
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

                    // reject connections that aren't from verified app instances
                    if(!GetConnectionRequest((IPEndPoint)tcpClient.RemoteEndPoint))
                    {
                        tcpClient.Close();
                        continue;
                    }

                    IPEndPoint udpEndPoint = new IPEndPoint(((IPEndPoint)tcpClient.RemoteEndPoint).Address, ClientUdpPort);
                    var hash = (UInt32)_rand.Next();

                    var clientData = new ClientData() {
                        id = ClientId.New(), 
                        hash = hash, 
                        tcpPacket = new Packet(BufferSize), 
                        tpcSocket = tcpClient,
                        udpPacket = new Packet(BufferSize, true),
                        udpEndPoint = udpEndPoint
                    };
                    clientData.tcpPacket.header.owlTreeVer = OwlTreeVersion;
                    clientData.tcpPacket.header.appVer = AppVersion;
                    clientData.udpPacket.header.owlTreeVer = OwlTreeVersion;
                    clientData.udpPacket.header.appVer = AppVersion;

                    _clientData.Add(clientData);

                    OnClientConnected?.Invoke(clientData.id);

                    // send new client their id
                    var span = clientData.tcpPacket.GetSpan(LocalClientConnectLength);
                    LocalClientConnectEncode(span, clientData.id, clientData.hash);

                    foreach (var otherClient in _clientData)
                    {
                        if (otherClient.id == clientData.id) continue;

                        // notify clients of a new client in the next send
                        span = otherClient.tcpPacket.GetSpan(ClientMessageLength);
                        ClientConnectEncode(span, clientData.id);

                        // add existing clients to new client
                        span = clientData.tcpPacket.GetSpan(ClientMessageLength);
                        ClientConnectEncode(span, otherClient.id);
                    }
                    
                    clientData.tcpPacket.header.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    ApplySendSteps(clientData.tcpPacket);
                    var bytes = clientData.tcpPacket.GetPacket();
                    tcpClient.Send(bytes);
                    clientData.tcpPacket.Reset();
                }
                else if (socket == _udpServer) // receive client udp messages
                {
                    Array.Clear(ReadBuffer, 0, ReadBuffer.Length);
                    ReadPacket.Clear();

                    EndPoint source = new IPEndPoint(IPAddress.Any, 0);
                    int dataLen = -1;
                    try
                    {
                        dataLen = socket.ReceiveFrom(ReadBuffer, ref source);
                        ReadPacket.FromBytes(ReadBuffer, 0);

                        if (ReadPacket.header.appVer < MinAppVersion || ReadPacket.header.owlTreeVer < MinOwlTreeVersion)
                        {
                            throw new InvalidOperationException("Cannot accept packets from outdated OwlTree or app versions.");
                        }
                    }
                    catch { }

                    if (dataLen <= 0)
                    {
                        continue;
                    }

                    var client = FindClientData((IPEndPoint)source);

                    // try to verify a new client connection
                    if (client == ClientData.None)
                    {
                        var accepted = false;
                        ReadPacket.StartMessageRead();
                        if (ReadPacket.TryGetNextMessage(out var bytes))
                        {
                            var rpcId = ServerMessageDecode(bytes, out var id);
                            if (rpcId == RpcId.CONNECTION_REQUEST && id == ApplicationId)
                            {

                                // connection request verified, send client confirmation
                                _connectionRequests.Add((IPEndPoint)source);
                                accepted = true;

                                ReadPacket.Clear();
                                ReadPacket.header.owlTreeVer = OwlTreeVersion;
                                ReadPacket.header.appVer = AppVersion;
                                ReadPacket.header.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                                ReadPacket.header.sender = 0;
                                ReadPacket.header.hash = 0;
                                var confirmation = ReadPacket.GetPacket();
                                _udpServer.SendTo(confirmation.ToArray(), source);
                            }
                        }

                        if (Logger.includes.connectionAttempts)
                        {
                            Logger.Write("Connection attempt from " + ((IPEndPoint)source).Address.ToString() + (accepted ? " accepted, awaiting TCP handshake..." : " rejected."));
                        }
                        continue;
                    }

                    if (Logger.includes.udpPreTransform)
                    {
                        var packetStr = new StringBuilder($"Pre-Transform UDP packet received from {client.id} at {DateTime.UtcNow}:\n");
                        PacketToString(ReadPacket, packetStr);
                        Logger.Write(packetStr.ToString());
                    }

                    ApplyReadSteps(ReadPacket);

                    if (Logger.includes.udpPostTransform)
                    {
                        var packetStr = new StringBuilder($"Post-Transform UDP packet received from {client.id} at {DateTime.UtcNow}:\n");
                        PacketToString(ReadPacket, packetStr);
                        Logger.Write(packetStr.ToString());
                    }

                    ReadPacket.StartMessageRead();
                    while (ReadPacket.TryGetNextMessage(out var bytes))
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
                    int dataRemaining = -1;
                    int dataLen = -1;
                    ClientData client = ClientData.None;

                    do {
                        ReadPacket.Clear();

                        int iters = 0;
                        do {
                            if (dataRemaining <= 0)
                            {
                                try
                                {
                                    dataLen = socket.Receive(ReadBuffer);
                                    dataRemaining = dataLen;
                                }
                                catch
                                {
                                    dataLen = -1;
                                    break;
                                }
                            }
                            dataRemaining -= ReadPacket.FromBytes(ReadBuffer, dataLen - dataRemaining);
                            iters++;
                        } while (ReadPacket.Incomplete && iters < 10);

                        if (ReadPacket.header.appVer < MinAppVersion || ReadPacket.header.owlTreeVer < MinOwlTreeVersion)
                        {
                            dataLen = -1;
                        }

                        if (client == ClientData.None)
                            client = FindClientData(socket);

                        // disconnect if receive fails
                        if (dataLen <= 0)
                        {
                            _clientData.Remove(client);
                            socket.Close();
                            OnClientDisconnected?.Invoke(client.id);

                            foreach (var otherClient in _clientData)
                            {
                                var span = otherClient.tcpPacket.GetSpan(ClientMessageLength);
                                ClientDisconnectEncode(span, client.id);
                            }
                            continue;
                        }

                        if (Logger.includes.tcpPreTransform)
                        {
                            var packetStr = new StringBuilder($"Pre-Transform TCP packet received from {client.id} at {DateTime.UtcNow}:\n");
                            PacketToString(ReadPacket, packetStr);
                            Logger.Write(packetStr.ToString());
                        }

                        ApplyReadSteps(ReadPacket);

                        if (Logger.includes.tcpPostTransform)
                        {
                            var packetStr = new StringBuilder($"Post-Transform TCP packet received from {client.id} at {DateTime.UtcNow}:\n");
                            PacketToString(ReadPacket, packetStr);
                            Logger.Write(packetStr.ToString());
                        }
                        
                        ReadPacket.StartMessageRead();
                        while (ReadPacket.TryGetNextMessage(out var bytes))
                        {
                            if (TryDecode(client.id, bytes, out var message))
                            {
                                _incoming.Enqueue(message);
                            }
                        }
                    } while (dataRemaining > 0);
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
                            Encode(message, client.tcpPacket);
                        else
                            Encode(message, client.udpPacket);
                    }
                }
                else
                {
                    if (message.protocol == Protocol.Tcp)
                    {
                        foreach (var client in _clientData)
                        {
                            Encode(message, client.tcpPacket);
                        }
                    }
                    else
                    {
                        foreach (var client in _clientData)
                        {
                            Encode(message, client.udpPacket);
                        }
                    }
                }
            }
            foreach (var client in _clientData)
            {
                if (!client.tcpPacket.IsEmpty)
                {
                    client.tcpPacket.header.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    if (Logger.includes.tcpPreTransform)
                    {
                        var packetStr = new StringBuilder($"Pre-Transform TCP packet sent to {client.id} at {DateTime.UtcNow}:\n");
                        PacketToString(client.tcpPacket, packetStr);
                        Logger.Write(packetStr.ToString());
                    }

                    ApplySendSteps(client.tcpPacket);
                    var bytes = client.tcpPacket.GetPacket();

                    if (Logger.includes.tcpPostTransform)
                    {
                        var packetStr = new StringBuilder($"Post-Transform TCP packet sent to {client.id} at {DateTime.UtcNow}:\n");
                        PacketToString(client.tcpPacket, packetStr);
                        Logger.Write(packetStr.ToString());
                    }

                    client.tpcSocket.Send(bytes);
                    client.tcpPacket.Reset();
                }

                if (!client.udpPacket.IsEmpty)
                {
                    client.udpPacket.header.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    if (Logger.includes.tcpPreTransform)
                    {
                        var packetStr = new StringBuilder($"Pre-Transform UDP packet sent to {client.id} at {DateTime.UtcNow}:\n");
                        PacketToString(client.udpPacket, packetStr);
                        Logger.Write(packetStr.ToString());
                    }

                    ApplySendSteps(client.udpPacket);
                    var bytes = client.udpPacket.GetPacket();

                    if (Logger.includes.tcpPostTransform)
                    {
                        var packetStr = new StringBuilder($"Post-Transform UDP packet sent to {client.id} at {DateTime.UtcNow}:\n");
                        PacketToString(client.udpPacket, packetStr);
                        Logger.Write(packetStr.ToString());
                    }

                    _udpServer.SendTo(bytes.ToArray(), client.udpEndPoint);
                    client.udpPacket.Reset();
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
                    var span = otherClient.tcpPacket.GetSpan(ClientMessageLength);
                    ClientDisconnectEncode(span, client.id);
                }
            }
        }
    }
}