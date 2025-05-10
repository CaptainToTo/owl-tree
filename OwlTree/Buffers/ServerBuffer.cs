
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
        public ServerBuffer(Args args, int maxClients, long requestTimeout, IPAddress[] whitelist) : base (args)
        {
            IPEndPoint tpcEndPoint = new IPEndPoint(IPAddress.Any, ServerTcpPort);
            _tcpServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _tcpServer.Bind(tpcEndPoint);
            _tcpServer.Listen(maxClients);
            ServerTcpPort = ((IPEndPoint)_tcpServer.LocalEndPoint).Port;
            _readList.Add(_tcpServer);

            IPEndPoint udpEndPoint = new IPEndPoint(IPAddress.Any, ServerUdpPort);
            _udpServer = new RudpServerSocket(udpEndPoint);
            ServerUdpPort = _udpServer.Port;
            _readList.Add(_udpServer.Socket);

            _clientData = new ClientDataList(BufferSize, DateTimeOffset.UtcNow.Millisecond);
            _whitelist = whitelist;

            MaxClients = maxClients == -1 ? int.MaxValue : maxClients;
            _requests = new(MaxClients, requestTimeout);
            LocalId = ClientId.None;
            Authority = ClientId.None;
            IsReady = true;
            AddReadyMessage(LocalId);
        }

        ~ServerBuffer()
        {
            if (_tcpServer.Connected)
                Disconnect();
        }

        public override int LocalTcpPort() => ServerTcpPort;

        public override int LocalUdpPort() => ServerUdpPort;

        public override int Latency() => _clientData.FindWorstLatency()?.latency ?? 0;

        // server state
        private Socket _tcpServer;
        private RudpServerSocket _udpServer;
        private List<Socket> _readList = new List<Socket>();
        private ClientDataList _clientData;
        private ConnectionRequestList _requests;
        private IPAddress[] _whitelist = null;

        private bool HasWhitelist => _whitelist != null && _whitelist.Length > 0;

        private bool IsOnWhitelist(IPAddress addr)
        {
            if (!HasWhitelist) return false;
            foreach (var a in _whitelist)
                if (a.Equals(addr)) return true;
            return false;
        }

        /// <summary>
        /// Reads any data currently on sockets. Putting new messages in the queue, and connecting new clients.
        /// </summary>
        public override void Recv()
        {
            _readList.Clear();
            _readList.Add(_tcpServer);
            _readList.Add(_udpServer.Socket);
            foreach (var data in _clientData)
                _readList.Add(data.tcpSocket);
            
            Socket.Select(_readList, null, null, 0);

            _requests.ClearTimeouts();

            _pingRequests.ClearTimeouts(PingTimeout);

            foreach (var socket in _readList)
            {
                // new client connects
                if (socket == _tcpServer)
                {
                    var tcpClient = socket.Accept();

                    // reject connections that aren't from verified app instances
                    if (!_requests.TryGet((IPEndPoint)tcpClient.RemoteEndPoint, out var udpPort, out long timestamp))
                    {
                        tcpClient.Close();
                        continue;
                    }

                    IPEndPoint udpEndPoint = new IPEndPoint(((IPEndPoint)tcpClient.RemoteEndPoint).Address, udpPort);

                    var clientData = _clientData.Add(tcpClient, udpEndPoint);
                    clientData.tcpPacket.header.owlTreeVer = OwlTreeVersion;
                    clientData.tcpPacket.header.appVer = AppVersion;
                    clientData.udpPacket.header.owlTreeVer = OwlTreeVersion;
                    clientData.udpPacket.header.appVer = AppVersion;
                    clientData.latency = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - timestamp);
                    _udpServer.AddEndpoint(udpEndPoint);

                    if (Logger.includes.connectionAttempts)
                    {
                        Logger.Write($"TCP handshake made with {((IPEndPoint)tcpClient.RemoteEndPoint).Address} (tcp port: {((IPEndPoint)tcpClient.RemoteEndPoint).Port}) (udp port: {udpPort}). Assigned: {clientData.id}");
                    }

                    AddClientConnectedMessage(clientData.id);

                    // send new client their id
                    var span = clientData.tcpPacket.GetSpan(LocalClientConnectLength);
                    LocalClientConnectEncode(span, new ClientIdAssignment(clientData.id, Authority, clientData.hash, MaxClients));

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
                    HasClientEvent = true;
                    
                    clientData.tcpPacket.header.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    ApplySendSteps(clientData.tcpPacket);
                    var bytes = clientData.tcpPacket.GetPacket();
                    tcpClient.Send(bytes);
                    clientData.tcpPacket.Reset();
                }
                else if (socket == _udpServer.Socket) // receive client udp messages
                {
                    while (_udpServer.Available > 0)
                    {
                        Array.Clear(ReadBuffer, 0, ReadBuffer.Length);

                        IPEndPoint source = new IPEndPoint(IPAddress.Any, 0);
                        int dataLen = -1;
                        RudpResult result = RudpResult.Failed;
                        try
                        {
                            result = _udpServer.ReceiveFrom(ReadBuffer, ref source, out dataLen);
                            ReadPacket.FromBytes(ReadBuffer, 0, dataLen);

                            if (ReadPacket.header.appVer < MinAppVersion || ReadPacket.header.owlTreeVer < MinOwlTreeVersion)
                            {
                                throw new InvalidOperationException("Cannot accept packets from outdated OwlTree or app versions.");
                            }
                        }
                        catch { }

                        if (dataLen <= 0)
                        {
                            break;
                        }

                        if (result == RudpResult.PingRequest)
                        {
                            var client = _clientData.Find(source);

                            if (client == null)
                            {
                                if (Logger.includes.exceptions)
                                    Logger.Write($"Ping request received from an unknown client. Ignoring packet.");
                                continue;
                            }
                
                            if (client.hash != ReadPacket.header.hash)
                            {
                                if (Logger.includes.exceptions)
                                    Logger.Write($"Incorrect hash received in UDP ping request from client {client.id}. Got {ReadPacket.header.hash}, but expected {client.hash}. Ignoring packet.");
                                continue;
                            }

                            ReadPacket.StartMessageRead();
                            if (ReadPacket.TryGetNextMessage(out var bytes))
                            {
                                try
                                {
                                    var rpcId = new RpcId(bytes);

                                    if (rpcId.Id == RpcId.PingRequestId && TryPingRequestDecode(bytes, out var request))
                                        HandlePingRequest(request, Protocol.Udp);
                                }
                                catch (Exception e)
                                {
                                    if (Logger.includes.exceptions)
                                        Logger.Write($"FAILED to handle UDP ping request '{BitConverter.ToString(bytes.ToArray())}' from {client.id}. Exception thrown:\n{e}");
                                }
                            }
                        }
                        // try to verify a new client connection
                        else if (result == RudpResult.UnregisteredEndpoint)
                        {
                            if (HasWhitelist && !IsOnWhitelist(source.Address))
                                continue;

                            if (Logger.includes.connectionAttempts)
                            {
                                Logger.Write("Connection attempt from " + source.Address.ToString() + " (udp port: " + source.Port + ") received: \n" + PacketToString(ReadPacket));
                            }

                            ConnectionResponseCode responseCode = ConnectionResponseCode.Accepted;
                            ReadPacket.StartMessageRead();
                            if (ReadPacket.TryGetNextMessage(out var bytes))
                            {
                                var rpcId = ServerMessageDecode(bytes, out var request);


                                if (rpcId != RpcId.ConnectionRequestId)
                                    responseCode = ConnectionResponseCode.Rejected;
                                else if (request.appId != ApplicationId)
                                    responseCode = ConnectionResponseCode.IncorrectAppId;
                                else if (request.sessionId != SessionId)
                                    responseCode = ConnectionResponseCode.IncorrectSessionId;
                                else if (_clientData.Count >= MaxClients || _requests.Count >= MaxClients)
                                    responseCode = ConnectionResponseCode.SessionFull;
                                else if (request.simulationSystem != SimulationSystem || request.tickRate != TickRate)
                                    responseCode = ConnectionResponseCode.IncorrectSimulationControl;
                                else if (request.isHost)
                                    responseCode = ConnectionResponseCode.Rejected;

                                // connection request verified, send client confirmation
                                if (responseCode == ConnectionResponseCode.Accepted)
                                {
                                    _requests.Add(source);
                                }
                                
                            }
                            else
                            {
                                responseCode = ConnectionResponseCode.Rejected;
                            }

                            ReadPacket.Clear();
                            ReadPacket.header.owlTreeVer = OwlTreeVersion;
                            ReadPacket.header.appVer = AppVersion;
                            ReadPacket.header.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                            ReadPacket.header.sender = 0;
                            ReadPacket.header.hash = 0;
                            var response = ReadPacket.GetSpan(4);
                            BitConverter.TryWriteBytes(response, (int)responseCode);
                            var responsePacket = ReadPacket.GetPacket();
                            _udpServer.SendTo(responsePacket.ToArray(), source);

                            if (Logger.includes.connectionAttempts)
                            {
                                string resultStr = "accepted, awaiting TCP handshake...";
                                switch (responseCode)
                                {
                                    case ConnectionResponseCode.Accepted:
                                        break;
                                    case ConnectionResponseCode.SessionFull: resultStr = "rejected, the session is full.";
                                        break;
                                    case ConnectionResponseCode.IncorrectAppId: resultStr = "rejected, the client gave the incorrect app id.";
                                        break;
                                    case ConnectionResponseCode.IncorrectSessionId: resultStr = "rejected, the client gave the incorrect session id.";
                                        break;
                                    case ConnectionResponseCode.IncorrectSimulationControl: resultStr = "rejected, the client is using the incorrect simulation system.";
                                        break;
                                    case ConnectionResponseCode.HostAlreadyAssigned: resultStr = "rejected, the client tried to claim the host role, but the host is already assigned.";
                                        break;
                                    case ConnectionResponseCode.Rejected: resultStr = "rejected.";
                                        break;
                                }
                                Logger.Write("Connection attempt from " + source.Address.ToString() + " (udp port: " + source.Port + ") " + resultStr);
                            }

                            continue;
                        }
                    }
                }
                else // receive client tcp messages
                {
                    Array.Clear(ReadBuffer, 0, ReadBuffer.Length);
                    int dataRemaining = -1;
                    int dataLen = -1;
                    ClientData client = null;

                    if (!socket.Connected)
                    {
                        client = _clientData.Find(socket);
                        Disconnect(client);
                        continue;
                    }

                    do {
                        ReadPacket.Clear();

                        int iters = 0;
                        do {
                            try
                            {
                                if (dataRemaining <= 0)
                                {
                                    dataLen = socket.Receive(ReadBuffer);
                                    dataRemaining = dataLen;
                                }
                                dataRemaining -= ReadPacket.FromBytes(ReadBuffer, dataLen - dataRemaining, dataLen);
                                iters++;
                            }
                            catch
                            {
                                dataLen = -1;
                                break;
                            }
                        } while (ReadPacket.Incomplete && iters < 10);

                        if (ReadPacket.header.appVer < MinAppVersion || ReadPacket.header.owlTreeVer < MinOwlTreeVersion)
                        {
                            dataLen = -1;
                        }

                        // disconnect if receive fails
                        if (dataLen <= 0)
                        {
                            Disconnect(_clientData.Find(socket));
                            break;
                        }

                        if (client == null)
                        {
                            client = _clientData.Find(socket);

                            if (client.hash != ReadPacket.header.hash)
                            {
                                if (Logger.includes.exceptions)
                                    Logger.Write($"Incorrect hash received in TCP packet from client {client.id}. Got {ReadPacket.header.hash}, but expected {client.hash}. Ignoring packet.");
                                continue;
                            }

                            client.latency = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - ReadPacket.header.timestamp);
                        }

                        if (Logger.includes.tcpPostTransform)
                        {
                            var packetStr = new StringBuilder($"RECEIVED: mutated Post-Transform TCP packet from {client.id}:\n");
                            PacketToString(ReadPacket, packetStr);
                            Logger.Write(packetStr.ToString());
                        }

                        ApplyReadSteps(ReadPacket);

                        if (Logger.includes.tcpPreTransform)
                        {
                            var packetStr = new StringBuilder($"RECEIVED: original Pre-Transform TCP packet from {client.id}:\n");
                            PacketToString(ReadPacket, packetStr);
                            Logger.Write(packetStr.ToString());
                        }
                        
                        ReadPacket.StartMessageRead();
                        while (ReadPacket.TryGetNextMessage(out var bytes))
                        {
                            try
                            {
                                if (TryPingRequestDecode(bytes, out var request))
                                {
                                    HandlePingRequest(request, Protocol.Tcp);
                                }
                                else
                                {
                                    Decode(client.id, bytes, Protocol.Tcp);
                                }
                            }
                            catch (Exception e)
                            {
                                if (Logger.includes.exceptions)
                                    Logger.Write($"FAILED to decode TCP message '{BitConverter.ToString(bytes.ToArray())}' from {client.id}. Exception thrown:\n{e}");
                            }
                        }
                    } while (dataRemaining > 0);
                }
            }
            
            _udpServer.RequestMissingPackets();

            while (_udpServer.TryGetNextPacket(out var packet, out var source))
            {
                ReadPacket.Clear();
                ReadPacket.FromBytes(packet, 0, packet.Length);

                var client = _clientData.Find(source);
                
                if (client.hash != ReadPacket.header.hash)
                {
                    if (Logger.includes.exceptions)
                        Logger.Write($"Incorrect hash received in UDP packet from client {client.id}. Got {ReadPacket.header.hash}, but expected {client.hash}. Ignoring packet.");
                    continue;
                }

                if (Logger.includes.udpPostTransform)
                {
                    var packetStr = new StringBuilder($"RECEIVED: mutated Post-Transform UDP packet from {client.id}:\n");
                    PacketToString(ReadPacket, packetStr);
                    Logger.Write(packetStr.ToString());
                }

                ApplyReadSteps(ReadPacket);

                if (Logger.includes.udpPreTransform)
                {
                    var packetStr = new StringBuilder($"RECEIVED: original Post-Transform UDP packet from {client.id}:\n");
                    PacketToString(ReadPacket, packetStr);
                    Logger.Write(packetStr.ToString());
                }

                ReadPacket.StartMessageRead();
                while (ReadPacket.TryGetNextMessage(out var bytes))
                {
                    try
                    {
                        if (TryPingRequestDecode(bytes, out var request))
                        {
                            HandlePingRequest(request, Protocol.Udp);
                        }
                        else
                        {
                            Decode(client.id, bytes, Protocol.Udp);
                        }
                    }
                    catch (Exception e)
                    {
                        if (Logger.includes.exceptions)
                            Logger.Write($"FAILED to decode UDP message '{BitConverter.ToString(bytes.ToArray())}' from {client.id}. Exception thrown:\n{e}");
                    }
                }
            }
        }

        private void HandlePingRequest(PingRequest request, Protocol protocol = Protocol.Udp)
        {
            if (request.Target == LocalId)
            {
                var data = _clientData.Find(request.Source);
                ReadPacket.Clear();
                ReadPacket.header.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                ReadPacket.header.sender = 0;
                ReadPacket.header.hash = data.hash;
                ReadPacket.header.pingRequest = true;
                PingResponse(request, ReadPacket);
                if (protocol == Protocol.Udp)
                    _udpServer.SendTo(ReadPacket.GetPacket().ToArray(), data.udpEndPoint);
                else
                    data.tcpSocket.Send(ReadPacket.GetPacket().ToArray());
                HasClientEvent = true;
            }
            else if (request.Source == LocalId)
            {
                var original = _pingRequests.Find(request);
                if (original != null)
                {
                    original.PingReceivedAt(request.ReceiveTime);
                    original.PingResponded();
                    _pingRequests.Remove(original);
                    AddIncoming(new IncomingMessage{
                        caller = request.Source, 
                        callee = request.Target, 
                        rpcId = new RpcId(RpcId.PingRequestId), 
                        target = NetworkId.None, 
                        protocol = protocol, 
                        perms = RpcPerms.AnyToAll,
                        args = new object[]{original}
                    });
                }
            }
            else
            {
                var target = _clientData.Find(request.Target);
                var source = _clientData.Find(request.Source);
                if (target == null || source == null)
                    return;
                
                var packet = request.Received ? source.tcpPacket : target.tcpPacket;
                var span = packet.GetSpan(PingRequestLength);
                PingRequestEncode(span, request);
                HasClientEvent = true;
            }
        }

        /// <summary>
        /// Write current buffers to client sockets.
        /// Buffers are cleared after writing.
        /// </summary>
        public override void Send()
        {
            while (TryGetNextOutgoing(out var message))
            {
                if (HandleClientEvent(message))
                    continue;
                
                if (message.rpcId == RpcId.PingRequestId && TryPingRequestDecode(message.bytes, out var request))
                {
                    var data = _clientData.Find(message.callee);

                    if (data == null)
                        continue;

                    var original = _pingRequests.Find(request);
                    original.PingSent();
                    PingRequestEncode(message.bytes, original);

                    ReadPacket.Clear();
                    ReadPacket.header.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    ReadPacket.header.sender = 0;
                    ReadPacket.header.hash = data.hash;
                    ReadPacket.header.pingRequest = true;
                    AddToPacket(message, ReadPacket);


                    if (message.protocol == Protocol.Udp)
                        _udpServer.SendTo(ReadPacket.GetPacket().ToArray(), data.udpEndPoint);
                    else
                        data.tcpSocket.Send(ReadPacket.GetPacket().ToArray());
                    continue;
                }

                if (message.callee != ClientId.None)
                {
                    var client = _clientData.Find(message.callee);
                    if (client != null)
                    {
                        Packet p = message.protocol == Protocol.Tcp ? client.tcpPacket : client.udpPacket;
                        AddToPacket(message, p);
                    }
                }
                else
                {
                    if (message.protocol == Protocol.Tcp)
                    {
                        foreach (var client in _clientData)
                        {
                            if (message.caller == client.id) continue;
                            AddToPacket(message, client.tcpPacket);
                        }
                    }
                    else
                    {
                        foreach (var client in _clientData)
                        {
                            if (message.caller == client.id) continue;
                            AddToPacket(message, client.udpPacket);
                        }
                    }
                }
            }
            foreach (var client in _clientData)
            {
                while (!client.tcpPacket.IsEmpty)
                {
                    client.tcpPacket.header.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    if (Logger.includes.tcpPreTransform)
                    {
                        var packetStr = new StringBuilder($"SENDING: Pre-Transform TCP packet to {client.id}:\n");
                        PacketToString(client.tcpPacket, packetStr);
                        Logger.Write(packetStr.ToString());
                    }

                    ApplySendSteps(client.tcpPacket);
                    var bytes = client.tcpPacket.GetPacket();

                    if (Logger.includes.tcpPostTransform)
                    {
                        var packetStr = new StringBuilder($"SENDING: Post-Transform TCP packet to {client.id}:\n");
                        PacketToString(client.tcpPacket, packetStr);
                        Logger.Write(packetStr.ToString());
                    }

                    try
                    {
                        client.tcpSocket.Send(bytes);
                    }
                    catch (Exception e)
                    {
                        if (Logger.includes.exceptions)
                            Logger.Write($"FAILED to send TCP packet to {client.id}. Exception thrown:\n{e}");
                    }
                    client.tcpPacket.Reset();
                }

                while (!client.udpPacket.IsEmpty)
                {
                    client.udpPacket.header.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    if (Logger.includes.tcpPreTransform)
                    {
                        var packetStr = new StringBuilder($"SENDING: Pre-Transform UDP packet to {client.id}:\n");
                        PacketToString(client.udpPacket, packetStr);
                        Logger.Write(packetStr.ToString());
                    }

                    ApplySendSteps(client.udpPacket);
                    var bytes = client.udpPacket.GetPacket();

                    if (Logger.includes.tcpPostTransform)
                    {
                        var packetStr = new StringBuilder($"SENDING: Post-Transform UDP packet to {client.id}:\n");
                        PacketToString(client.udpPacket, packetStr);
                        Logger.Write(packetStr.ToString());
                    }

                    try
                    {
                        _udpServer.SendTo(bytes.ToArray(), client.udpEndPoint);
                    }
                    catch (Exception e)
                    {
                        if (Logger.includes.exceptions)
                            Logger.Write($"FAILED to send UDP packet to {client.id}. Exception throw:\n{e}");
                    }
                    client.udpPacket.Reset();
                }
            }

            HasClientEvent = false;
        }

        /// <summary>
        /// Disconnects all clients, and closes the server.
        /// </summary>
        public override void Disconnect()
        {
            if (!_tcpServer.Connected)
                return;
            var ids = _clientData.GetIds();
            foreach (var id in ids)
            {
                Disconnect(id);
            }
            _tcpServer.Close();
            _udpServer.Close();
            IsReady = false;
            IsActive = false;
            AddClientDisconnectedMessage(LocalId);
        }


        /// <summary>
        /// Disconnect a client from the server.
        /// Invokes <c>OnClientDisconnected</c>.
        /// </summary>
        public override void Disconnect(ClientId id)
        {
            var client = _clientData.Find(id);
            if (client != null)
                Disconnect(client);
        }

        private void Disconnect(ClientData client)
        {
            _clientData.Remove(client);
            _udpServer.RemoveEndpoint(client.udpEndPoint);
            client.tcpSocket.Close();
            AddClientDisconnectedMessage(client.id);

            foreach (var otherClient in _clientData)
            {
                var span = otherClient.tcpPacket.GetSpan(ClientMessageLength);
                ClientDisconnectEncode(span, client.id);
            }
            HasClientEvent = true;
        }

        public override void MigrateHost(ClientId newHost)
        {
            throw new InvalidOperationException("Servers cannot migrate authority off of themselves.");
        }
    }
}