
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
        public RelayBuffer(Args args, int maxClients, long requestTimeout, string hostAddr, bool migratable, IPAddress[] whitelist) : base(args)
        {
            IPEndPoint tpcEndPoint = new IPEndPoint(IPAddress.Any, TcpPort);
            _tcpRelay = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _tcpRelay.Bind(tpcEndPoint);
            _tcpRelay.Listen(maxClients);
            _readList.Add(_tcpRelay);

            IPEndPoint udpEndPoint = new IPEndPoint(IPAddress.Any, ServerUdpPort);
            _udpRelay = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _udpRelay.Bind(udpEndPoint);
            _readList.Add(_udpRelay);

            _whitelist = whitelist;

            if (hostAddr != null)
                _hostAddr = IPAddress.Parse(hostAddr);
            Migratable = migratable;

            _clientData = new ClientDataList(BufferSize, DateTimeOffset.UtcNow.Millisecond);

            MaxClients = maxClients == -1 ? int.MaxValue : maxClients;
            _requests = new(MaxClients, requestTimeout);
            LocalId = ClientId.None;
            Authority = ClientId.None;
            IsReady = true;
            OnReady?.Invoke(LocalId);
        }

        /// <summary>
        /// The maximum number of clients allowed to be connected at once in this session.
        /// </summary>
        public int MaxClients { get; private set; }

        // server state
        private Socket _tcpRelay;
        private Socket _udpRelay;
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

        public override void Read()
        {
            if (!IsActive)
                return;
            
            _readList.Clear();
            _readList.Add(_tcpRelay);
            _readList.Add(_udpRelay);
            foreach (var data in _clientData)
                _readList.Add(data.tcpSocket);
            
            Socket.Select(_readList, null, null, 0);

            _requests.ClearTimeouts();

            foreach (var socket in _readList)
            {
                // new client connects
                if (socket == _tcpRelay)
                {
                    var tcpClient = socket.Accept();

                    // reject connections that aren't from verified app instances
                    if(!_requests.TryGet((IPEndPoint)tcpClient.RemoteEndPoint, out var udpPort))
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

                    OnClientConnected?.Invoke(clientData.id);

                    // send new client their id
                    var span = clientData.tcpPacket.GetSpan(LocalClientConnectLength);
                    LocalClientConnectEncode(span, new ClientIdAssignment(clientData.id, Authority, clientData.hash));

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
                else if (socket == _udpRelay) // receive client udp messages
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

                    var client = _clientData.Find((IPEndPoint)source);

                    // try to verify a new client connection
                    if (client == ClientData.None)
                    {
                        var accepted = false;

                        if (HasWhitelist && !IsOnWhitelist(((IPEndPoint)source).Address))
                            continue;

                        if (Logger.includes.connectionAttempts)
                        {
                            Logger.Write("Connection attempt from " + ((IPEndPoint)source).Address.ToString() + " (udp port: " + ((IPEndPoint)source).Port + ") received: \n" + PacketToString(ReadPacket));
                        }

                        // if the pre-assigned host hasn't connected yet, no-one else can join 
                        if (_hostAddr != null && Authority == ClientId.None && !_hostAddr.Equals(((IPEndPoint)source).Address))
                        {
                            Logger.Write("Connection attempt from " + ((IPEndPoint)source).Address.ToString() + " (udp port: " + ((IPEndPoint)source).Port + ") rejected.");
                            continue;
                        }

                        ReadPacket.StartMessageRead();
                        if (ReadPacket.TryGetNextMessage(out var bytes))
                        {
                            var rpcId = ServerMessageDecode(bytes, out var request);
                            if (
                                rpcId == RpcId.CONNECTION_REQUEST && 
                                request.appId == ApplicationId && !request.isHost &&
                                _clientData.Count < MaxClients && _requests.Count < MaxClients
                            )
                            {

                                // connection request verified, send client confirmation
                                _requests.Add((IPEndPoint)source);
                                accepted = true;
                            }
                            
                            ReadPacket.Clear();
                            ReadPacket.header.owlTreeVer = OwlTreeVersion;
                            ReadPacket.header.appVer = AppVersion;
                            ReadPacket.header.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                            ReadPacket.header.sender = 0;
                            ReadPacket.header.hash = 0;
                            var response = ReadPacket.GetSpan(4);
                            BitConverter.TryWriteBytes(response, (int)(accepted ? ConnectionResponseCode.Accepted : ConnectionResponseCode.Rejected));
                            var responsePacket = ReadPacket.GetPacket();
                            _udpRelay.SendTo(responsePacket.ToArray(), source);
                        }

                        if (Logger.includes.connectionAttempts)
                        {
                            Logger.Write("Connection attempt from " + ((IPEndPoint)source).Address.ToString() + " (udp port: " + ((IPEndPoint)source).Port + ") " + (accepted ? "accepted, awaiting TCP handshake..." : "rejected."));
                        }
                        continue;
                    }

                    if (Logger.includes.udpPreTransform)
                    {
                        var packetStr = new StringBuilder($"RECEIVED: Pre-Transform UDP packet from {client.id} at {DateTime.UtcNow}:\n");
                        PacketToString(ReadPacket, packetStr);
                        Logger.Write(packetStr.ToString());
                    }

                    ApplyReadSteps(ReadPacket);

                    if (Logger.includes.udpPostTransform)
                    {
                        var packetStr = new StringBuilder($"RECEIVED: Post-Transform UDP packet from {client.id} at {DateTime.UtcNow}:\n");
                        PacketToString(ReadPacket, packetStr);
                        Logger.Write(packetStr.ToString());
                    }

                    ReadPacket.StartMessageRead();
                    while (ReadPacket.TryGetNextMessage(out var bytes))
                    {
                        var rpcId = new RpcId(bytes);
                        if (rpcId >= RpcId.FIRST_RPC_ID)
                        {
                            RpcEncoding.DecodeRpcHeader(bytes, out rpcId, out var caller, out var callee, out var target);
                            if (caller != client.id) continue;

                            if (callee == ClientId.None)
                                RelayUdpMessage(bytes, client.id);
                            else
                                RelayMessageTo(bytes, _clientData.Find(callee).udpPacket);
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
                            try
                            {
                                if (dataRemaining <= 0)
                                {
                                    dataLen = socket.Receive(ReadBuffer);
                                    dataRemaining = dataLen;
                                }
                                dataRemaining -= ReadPacket.FromBytes(ReadBuffer, dataLen - dataRemaining);
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

                        if (client == ClientData.None)
                            client = _clientData.Find(socket);

                        // disconnect if receive fails
                        if (dataLen <= 0)
                        {
                            Disconnect(client);
                            break;
                        }

                        if (Logger.includes.tcpPreTransform)
                        {
                            var packetStr = new StringBuilder($"RECEIVED: Pre-Transform TCP packet from {client.id} at {DateTime.UtcNow}:\n");
                            PacketToString(ReadPacket, packetStr);
                            Logger.Write(packetStr.ToString());
                        }

                        ApplyReadSteps(ReadPacket);

                        if (Logger.includes.tcpPostTransform)
                        {
                            var packetStr = new StringBuilder($"RECEIVED: Post-Transform TCP packet from {client.id} at {DateTime.UtcNow}:\n");
                            PacketToString(ReadPacket, packetStr);
                            Logger.Write(packetStr.ToString());
                        }
                        
                        ReadPacket.StartMessageRead();
                        while (ReadPacket.TryGetNextMessage(out var bytes))
                        {
                            var rpcId = new RpcId(bytes);

                            if (rpcId.Id == RpcId.CLIENT_DISCONNECTED_MESSAGE_ID && client.id == Authority)
                            {
                                Disconnect(new ClientId(bytes.Slice(rpcId.ByteLength())));
                            }
                            else if (rpcId.Id == RpcId.HOST_MIGRATION && client.id == Authority)
                            {
                                MigrateHost(new ClientId(bytes.Slice(rpcId.ByteLength())));
                            }
                            else if ((rpcId.Id == RpcId.NETWORK_OBJECT_SPAWN || rpcId.Id == RpcId.NETWORK_OBJECT_DESPAWN) && client.id == Authority)
                            {
                                RelayTcpMessage(bytes, client.id);
                            }
                            else if (rpcId.Id == RpcId.PING_REQUEST && TryPingRequestDecode(bytes, out var request))
                            {
                                HandlePingRequest(request);
                            }
                            else if (rpcId >= RpcId.FIRST_RPC_ID)
                            {
                                RpcEncoding.DecodeRpcHeader(bytes, out rpcId, out var caller, out var callee, out var target);
                                if (caller != client.id) continue;

                                if (callee == ClientId.None)
                                    RelayTcpMessage(bytes, client.id);
                                else
                                    RelayMessageTo(bytes, _clientData.Find(callee).tcpPacket);
                            }
                        }
                    } while (dataRemaining > 0);
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
        }

        private void HandlePingRequest(PingRequest request)
        {
            if (request.Target == LocalId)
            {
                PingResponse(request);
            }
            else if (request.Source == LocalId)
            {
                var original = _pingRequests.Find(request.Target);
                if (original != null)
                {
                    original.PingResponded();
                    _pingRequests.Remove(original);
                    _incoming.Enqueue(new Message(ClientId.None, LocalId, new RpcId(RpcId.PING_REQUEST), NetworkId.None, Protocol.Tcp, new object[]{original}));
                }
            }
            else
            {
                var target = _clientData.Find(request.Target);
                var source = _clientData.Find(request.Source);
                if (target == ClientData.None || source == ClientData.None)
                    return;
                
                var packet = request.Received ? source.tcpPacket : target.tcpPacket;
                var span = packet.GetSpan(PingRequestLength);
                PingRequestEncode(span, request);
                HasClientEvent = true;
            }
        }

        public override void Send()
        {
            while (_outgoing.TryDequeue(out var message))
            {

                if (message.callee != ClientId.None)
                {
                    var client = _clientData.Find(message.callee);
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
                        var packetStr = new StringBuilder($"SENDING: Pre-Transform TCP packet to {client.id} at {DateTime.UtcNow}:\n");
                        PacketToString(client.tcpPacket, packetStr);
                        Logger.Write(packetStr.ToString());
                    }

                    ApplySendSteps(client.tcpPacket);
                    var bytes = client.tcpPacket.GetPacket();

                    if (Logger.includes.tcpPostTransform)
                    {
                        var packetStr = new StringBuilder($"SENDING: Post-Transform TCP packet to {client.id} at {DateTime.UtcNow}:\n");
                        PacketToString(client.tcpPacket, packetStr);
                        Logger.Write(packetStr.ToString());
                    }

                    client.tcpSocket.Send(bytes);
                    client.tcpPacket.Reset();
                }

                if (!client.udpPacket.IsEmpty)
                {
                    client.udpPacket.header.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    if (Logger.includes.tcpPreTransform)
                    {
                        var packetStr = new StringBuilder($"SENDING: Pre-Transform UDP packet to {client.id} at {DateTime.UtcNow}:\n");
                        PacketToString(client.udpPacket, packetStr);
                        Logger.Write(packetStr.ToString());
                    }

                    ApplySendSteps(client.udpPacket);
                    var bytes = client.udpPacket.GetPacket();

                    if (Logger.includes.tcpPostTransform)
                    {
                        var packetStr = new StringBuilder($"SENDING: Post-Transform UDP packet to {client.id} at {DateTime.UtcNow}:\n");
                        PacketToString(client.udpPacket, packetStr);
                        Logger.Write(packetStr.ToString());
                    }

                    _udpRelay.SendTo(bytes.ToArray(), client.udpEndPoint);
                    client.udpPacket.Reset();
                }
            }

            HasClientEvent = false;
        }

        public override void Disconnect()
        {
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
            OnClientDisconnected?.Invoke(LocalId);
        }

        public override void Disconnect(ClientId id)
        {
            var client = _clientData.Find(id);
            if (client != ClientData.None)
            {
                Disconnect(client);
            }
        }

        private void Disconnect(ClientData client)
        {
            if (client == ClientData.None)
                return;
            
            _clientData.Remove(client);
            client.tcpSocket.Close();
            OnClientDisconnected?.Invoke(client.id);

            foreach (var otherClient in _clientData)
            {
                var span = otherClient.tcpPacket.GetSpan(ClientMessageLength);
                ClientDisconnectEncode(span, client.id);
            }
            HasClientEvent = true;

            if (client.id == Authority)
            {
                if (Migratable && _clientData.Count > 0)
                    MigrateHost(FindNewHost());
                else
                    Disconnect();
            }
        }

        // TODO: improve heuristic
        private ClientId FindNewHost()
        {
            foreach (var client in _clientData)
                if (client.id != Authority) return client.id;
            return ClientId.None;
        }

        /// <summary>
        /// Change the authority of the session to the given new host.
        /// The previous host will be down-graded to a client if they are still connected.
        /// </summary>
        public override void MigrateHost(ClientId newHost)
        {
            if (_clientData.Find(newHost) == ClientData.None)
                return;
            Authority = newHost;
            foreach (var client in _clientData)
            {
                var span = client.tcpPacket.GetSpan(ClientMessageLength);
                HostMigrationEncode(span, newHost);
            }
            HasClientEvent = true;
        }
    }
}