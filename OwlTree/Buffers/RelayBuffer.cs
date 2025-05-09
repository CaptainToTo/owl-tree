
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace OwlTree
{
    /// <summary>
    /// Manages passing packets between clients in a peer-to-peer session.
    /// </summary>
    public class RelayBuffer : NetworkBuffer
    {
        public RelayBuffer(Args args, int maxClients, long requestTimeout, string hostAddr, bool migratable, bool shutdownWhenEmpty, IPAddress[] whitelist) : base(args)
        {
            IPEndPoint tpcEndPoint = new IPEndPoint(IPAddress.Any, ServerTcpPort);
            _tcpRelay = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _tcpRelay.Bind(tpcEndPoint);
            _tcpRelay.Listen(maxClients);
            ServerTcpPort = ((IPEndPoint)_tcpRelay.LocalEndPoint).Port;
            _readList.Add(_tcpRelay);

            IPEndPoint udpEndPoint = new IPEndPoint(IPAddress.Any, ServerUdpPort);
            _udpRelay = new RudpServerSocket(udpEndPoint);
            ServerUdpPort = _udpRelay.Port;
            _readList.Add(_udpRelay.Socket);

            _whitelist = whitelist;

            ShutdownWhenEmpty = shutdownWhenEmpty;
            if (hostAddr != null)
                _hostAddr = IPAddress.Parse(hostAddr);
            Migratable = ShutdownWhenEmpty ? migratable : true;

            _clientData = new ClientDataList(BufferSize, DateTimeOffset.UtcNow.Millisecond);

            MaxClients = maxClients == -1 ? int.MaxValue : maxClients;
            _requests = new(MaxClients, requestTimeout);
            LocalId = ClientId.None;
            Authority = ClientId.None;
            IsReady = true;
            AddReadyMessage(LocalId);
        }

        ~RelayBuffer()
        {
            if (_tcpRelay.Connected)
                Disconnect();
        }

        public override int LocalTcpPort() => ServerTcpPort;

        public override int LocalUdpPort() => ServerUdpPort;

        public override int Latency() => _clientData.FindWorstLatency()?.latency ?? 0;

        // server state
        private Socket _tcpRelay;
        private RudpServerSocket _udpRelay;
        private List<Socket> _readList = new();
        private ClientDataList _clientData;
        private ConnectionRequestList _requests;

        private IPAddress _hostAddr = null;

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
        /// Whether or not the host role can be migrated or not. 
        /// If not, then the relay server will shutdown when the host disconnects.
        /// </summary>
        public bool Migratable { get; private set; }

        /// <summary>
        /// Whether or not to shutdown the server if it is empty.
        /// </summary>
        public bool ShutdownWhenEmpty { get; private set; }

        public override void Recv()
        {
            if (!IsActive)
                return;
            
            _readList.Clear();
            _readList.Add(_tcpRelay);
            _readList.Add(_udpRelay.Socket);
            foreach (var data in _clientData)
                _readList.Add(data.tcpSocket);
            
            Socket.Select(_readList, null, null, 0);

            _requests.ClearTimeouts();

            _pingRequests.ClearTimeouts(PingTimeout);

            foreach (var socket in _readList)
            {
                // new client connects
                if (socket == _tcpRelay)
                {
                    var tcpClient = socket.Accept();

                    // reject connections that aren't from verified app instances
                    if(!_requests.TryGet((IPEndPoint)tcpClient.RemoteEndPoint, out var udpPort, out var timestamp))
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
                    _udpRelay.AddEndpoint(udpEndPoint);

                    if (Logger.includes.connectionAttempts)
                    {
                        Logger.Write($"TCP handshake made with {((IPEndPoint)tcpClient.RemoteEndPoint).Address} (tcp port: {((IPEndPoint)tcpClient.RemoteEndPoint).Port}) (udp port: {udpPort}). Assigned: {clientData.id}");
                    }

                    if (Authority == ClientId.None && (_hostAddr == null || _hostAddr.Equals(((IPEndPoint)tcpClient.RemoteEndPoint).Address)))
                    {
                        _hostAddr = ((IPEndPoint)tcpClient.RemoteEndPoint).Address;
                        Authority = clientData.id;

                        if (Logger.includes.connectionAttempts)
                        {
                            Logger.Write($"Client {clientData.id} assigned as host.");
                        }
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
                else if (socket == _udpRelay.Socket) // receive client udp messages
                {
                    while (_udpRelay.Available > 0)
                    {
                        Array.Clear(ReadBuffer, 0, ReadBuffer.Length);

                        IPEndPoint source = new IPEndPoint(IPAddress.Any, 0);
                        int dataLen = -1;
                        RudpResult result = RudpResult.Failed;
                        try
                        {
                            result = _udpRelay.ReceiveFrom(ReadBuffer, ref source, out dataLen);
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

                        // try to verify a new client connection
                        if (result == RudpResult.UnregisteredEndpoint)
                        {
                            if (HasWhitelist && !IsOnWhitelist(source.Address))
                                continue;

                            if (Logger.includes.connectionAttempts)
                            {
                                Logger.Write("Connection attempt from " + source.Address.ToString() + " (udp port: " + source.Port + ") received: \n" + PacketToString(ReadPacket));
                            }

                            // if the pre-assigned host hasn't connected yet, no-one else can join 
                            if (_hostAddr != null && Authority == ClientId.None && !_hostAddr.Equals(source.Address))
                            {
                                Logger.Write("Connection attempt from " + source.Address.ToString() + " (udp port: " + source.Port + ") rejected.");
                                continue;
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
                                else if (request.simulationSystem != SimulationSystem)
                                    responseCode = ConnectionResponseCode.IncorrectSimulationControl;
                                else if (request.isHost && Authority != ClientId.None)
                                    responseCode = ConnectionResponseCode.HostAlreadyAssigned;

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
                            _udpRelay.SendTo(responsePacket.ToArray(), source);

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

                        if (client == null)
                        {
                            client = _clientData.Find(socket);

                            if (client.hash != ReadPacket.header.hash)
                            {
                                if (Logger.includes.exceptions)
                                    Logger.Write($"Incorrect hash received in TCP packet from client {client.id}. Got {ReadPacket.header.hash}, but expected {client.hash}. Ignoring packet. Client has failed {client.failed} times, will disconnect at 10.");
                                client.failed++;
                                if (client.failed > 10)
                                {
                                    Disconnect(client);
                                    break;
                                }
                                else
                                    continue;
                            }
                            client.failed = 0;

                            var time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                            client.latency = (int)(time - ReadPacket.header.timestamp);
                            client.lastConfirmed = time;
                        }

                        // disconnect if receive fails
                        if (dataLen <= 0)
                        {
                            Disconnect(client);
                            break;
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
                                var rpcId = new RpcId(bytes);

                                if (rpcId.Id == RpcId.ClientDisconnectedId && client.id == Authority)
                                {
                                    Disconnect(new ClientId(bytes.Slice(rpcId.ByteLength())));
                                }
                                else if (rpcId.Id == RpcId.HostMigrationId && client.id == Authority)
                                {
                                    MigrateHost(new ClientId(bytes.Slice(rpcId.ByteLength())));
                                }
                                else if (rpcId.IsObjectEvent() && client.id == Authority)
                                {
                                    RelayTcpMessage(bytes, client.id);
                                }
                                else if (rpcId.Id == RpcId.PingRequestId && TryPingRequestDecode(bytes, out var request))
                                {
                                    HandlePingRequest(request);
                                }
                                else if (rpcId.IsTickEvent())
                                {
                                    SimulationBuffer.DecodeClients(bytes.Slice(RpcId.MaxByteLength), out var caller, out var callee);
                                    if (rpcId == RpcId.CurTickId && caller == client.id && client.id == Authority)
                                        RelayMessageTo(bytes, _clientData.Find(callee).tcpPacket);
                                    else if (rpcId == RpcId.NextTickId)
                                        RelayTcpMessage(bytes, client.id);
                                }
                                else if (rpcId >= RpcId.FirstRpcId)
                                {
                                    RpcEncoding.DecodeRpcHeader(bytes, out rpcId, out var caller, out var callee, out var target);
                                    if (caller != client.id) continue;

                                    if (callee == ClientId.None)
                                        RelayTcpMessage(bytes, client.id);
                                    else
                                        RelayMessageTo(bytes, _clientData.Find(callee).tcpPacket);
                                }
                            }
                            catch (Exception e)
                            {
                                if (Logger.includes.exceptions)
                                    Logger.Write($"FAILED to relay TCP message '{BitConverter.ToString(bytes.ToArray())}' from {client.id}. Exception thrown:\n{e}");
                            }
                        }
                    } while (dataRemaining > 0);
                }
            }

            _udpRelay.RequestMissingPackets();

            while (_udpRelay.TryGetNextPacket(out var packet, out var source))
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
                    var packetStr = new StringBuilder($"RECEIVED: original Pre-Transform UDP packet from {client.id}:\n");
                    PacketToString(ReadPacket, packetStr);
                    Logger.Write(packetStr.ToString());
                }

                ReadPacket.StartMessageRead();
                while (ReadPacket.TryGetNextMessage(out var bytes))
                {
                    try
                    {
                        var rpcId = new RpcId(bytes);
                        if (rpcId >= RpcId.FirstRpcId)
                        {
                            RpcEncoding.DecodeRpcHeader(bytes, out rpcId, out var caller, out var callee, out var target);
                            if (caller != client.id) continue;

                            if (callee == ClientId.None)
                                RelayUdpMessage(bytes, client.id);
                            else
                                RelayMessageTo(bytes, _clientData.Find(callee).udpPacket);
                        }
                        else if (rpcId == RpcId.NextTickId)
                            RelayUdpMessage(bytes, client.id);
                    }
                    catch (Exception e)
                    {
                        if (Logger.includes.exceptions)
                            Logger.Write($"FAILED to relay UDP message '{BitConverter.ToString(bytes.ToArray())}' from {client.id}. Exception thrown:\n{e}");
                    }
                }
            }
        }

        private void RelayTcpMessage(ReadOnlySpan<byte> bytes, ClientId source)
        {
            foreach (var client in _clientData)
            {
                if (client.id == source) continue;
                RelayMessageTo(bytes, client.tcpPacket);
            }
        }

        private void RelayUdpMessage(ReadOnlySpan<byte> bytes, ClientId source)
        {
            foreach (var client in _clientData)
            {
                if (client.id == source) continue;
                RelayMessageTo(bytes, client.udpPacket);
            }
        }

        private void RelayMessageTo(ReadOnlySpan<byte> bytes, Packet packet)
        {
            var span = packet.GetSpan(bytes.Length);
            for (int i = 0; i < span.Length; i++)
                span[i] = bytes[i];
            HasRelayMessages = true;
        }

        private void HandlePingRequest(PingRequest request)
        {
            if (request.Target == LocalId)
            {
                PingResponse(request, _clientData.Find(request.Source).tcpPacket);
                HasClientEvent = true;
            }
            else if (request.Source == LocalId)
            {
                var original = _pingRequests.Find(request.Target);
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
                        protocol = Protocol.Tcp,
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

        public override void Send()
        {
            while (TryGetNextOutgoing(out var message))
            {
                if (HandleClientEvent(message))
                    continue;
                
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
                            AddToPacket(message, client.tcpPacket);
                    }
                    else
                    {
                        foreach (var client in _clientData)
                            AddToPacket(message, client.udpPacket);
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
                        client.lastConfirmed = client.tcpPacket.header.timestamp;
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
                        _udpRelay.SendTo(bytes.ToArray(), client.udpEndPoint);
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
            HasRelayMessages = false;
        }

        public override void Disconnect()
        {
            if (!_tcpRelay.Connected)
                return;

            var ids = _clientData.GetIds();
            foreach (var id in ids)
            {
                if (id == Authority) continue;
                Disconnect(id);
            }
            Disconnect(Authority);
            _tcpRelay.Close();
            _udpRelay.Close();
            IsReady = false;
            IsActive = false;
            AddClientDisconnectedMessage(LocalId);
        }

        public override void Disconnect(ClientId id)
        {
            var client = _clientData.Find(id);
            if (client != null)
                Disconnect(client);
        }

        private void Disconnect(ClientData client)
        {
            if (client == null)
                return;
            
            // migrate before disconnect
            if (client.id == Authority && Migratable)
            {
                if (!ShutdownWhenEmpty && _clientData.Count <= 1)
                {
                    Authority = ClientId.None;
                    _hostAddr = null;
                    AddHostMigrationMessage(Authority);
                }
                else if (_clientData.Count > 1)
                {
                    MigrateHost(FindNewHost());
                }
            }
            
            _clientData.Remove(client);
            _udpRelay.RemoveEndpoint(client.udpEndPoint);
            client.tcpSocket.Close();
            AddClientDisconnectedMessage(client.id);

            foreach (var otherClient in _clientData)
            {
                var span = otherClient.tcpPacket.GetSpan(ClientMessageLength);
                ClientDisconnectEncode(span, client.id);
            }
            HasClientEvent = true;

            // shutdown after disconnect
            if (client.id == Authority)
            {
                if (!Migratable)
                    Disconnect();
                else if (ShutdownWhenEmpty && _clientData.Count <= 0)
                    Disconnect();
            }
        }

        // decide new host by lowest latency
        private ClientId FindNewHost()
        {
            ClientData best = null;
            foreach (var client in _clientData)
            {
                if (client.id == Authority) continue;
                if (best == null || client.latency < best.latency)
                    best = client;
            }
            return best?.id ?? ClientId.None;
        }

        /// <summary>
        /// Change the authority of the session to the given new host.
        /// The previous host will be down-graded to a client if they are still connected.
        /// </summary>
        public override void MigrateHost(ClientId newHost)
        {
            var data = _clientData.Find(newHost);
            if (data == null)
                return;
            Authority = newHost;
            _hostAddr = data.Address;
            foreach (var client in _clientData)
            {
                var span = client.tcpPacket.GetSpan(ClientMessageLength);
                HostMigrationEncode(span, newHost);
            }
            HasClientEvent = true;

            AddHostMigrationMessage(newHost);
        }
    }
}