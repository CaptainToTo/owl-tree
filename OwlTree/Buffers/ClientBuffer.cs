using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace OwlTree
{
    /// <summary>
    /// Manages sending and receiving messages for a client instance.
    /// </summary>
    public class ClientBuffer : NetworkBuffer
    {
        /// <summary>
        /// Manages sending and receiving messages for a client instance.
        /// </summary>
        /// <param name="Args">NetworkBuffer parameters.</param>
        public ClientBuffer(Args args, int requestRate, int requestLimit, bool requestAsHost) : base(args)
        {
            _tcpClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _tcpEndPoint = new IPEndPoint(Address, ServerTcpPort);

            _udpEndPoint = new IPEndPoint(Address, ServerUdpPort);

            _udpClient = new RudpClientSocket(new IPEndPoint(IPAddress.Any, 0), _udpEndPoint);

            _readList.Add(_tcpClient);
            _readList.Add(_udpClient.Socket);

            _tcpPacket = new Packet(BufferSize, true);
            _tcpPacket.header.owlTreeVer = OwlTreeVersion;
            _tcpPacket.header.appVer = AppVersion;
            _udpPacket = new Packet(BufferSize, true);
            _udpPacket.header.owlTreeVer = OwlTreeVersion;
            _udpPacket.header.appVer = AppVersion;

            _requestRate = requestRate;
            _remainingRequests = requestLimit;
            _requestAsHost = requestAsHost;
        }

        ~ClientBuffer()
        {
            if (_tcpClient.Connected)
                Disconnect();
        }

        // client state
        private Socket _tcpClient;
        private RudpClientSocket _udpClient;
        private IPEndPoint _tcpEndPoint;
        private IPEndPoint _udpEndPoint;
        private List<Socket> _readList = new List<Socket>();
        private List<ClientId> _clients = new List<ClientId>();

        public override int LocalTcpPort() => _tcpClient != null ? ((IPEndPoint)_tcpClient.LocalEndPoint).Port : 0;

        public override int LocalUdpPort() => _udpClient.Port;

        public override int Latency() => _latency;

        private int _latency = 0;

        private uint _hash = 0;

        // messages to be sent ot the sever
        private Packet _tcpPacket;
        private Packet _udpPacket;

        private bool _acceptedRequest = false;
        private int _requestRate;
        private long _lastRequest;
        private int _remainingRequests;
        private bool _requestAsHost;

        public void Connect()
        {
            if (_acceptedRequest) return;

            if (Timestamp.Now - _lastRequest > _requestRate)
            {
                try
                {
                    ConnectionRequestEncode(_udpPacket, new ConnectionRequest(ApplicationId, SessionId, _requestAsHost, SimulationSystem, TickRate));
                    _udpPacket.header.timestamp = Timestamp.Now;
                    _udpClient.SendTo(_udpPacket.GetPacket().ToArray(), _udpEndPoint);

                    if (Logger.includes.connectionAttempts)
                        Logger.Write($"Connection request made to {Address} (TCP: {ServerTcpPort}, UDP: {ServerUdpPort}) at {Timestamp.NowString}. Sent:\n{PacketToString(_udpPacket)}");

                    _udpPacket.Clear();
                }
                catch (Exception e)
                {
                    if (Logger.includes.exceptions)
                        Logger.Write($"Connection request made to {Address} (TCP: {ServerTcpPort}, UDP: {ServerUdpPort}) failed with the exception:\n{e}");
                }
                _lastRequest = Timestamp.Now;
                _remainingRequests--;
            }

            _readList.Clear();
            _readList.Add(_udpClient.Socket);
            Socket.Select(_readList, null, null, _requestRate);

            foreach (var socket in _readList)
            {
                if (socket == _udpClient.Socket)
                {
                    Array.Clear(ReadBuffer, 0, ReadBuffer.Length);
                    ReadPacket.Clear();

                    EndPoint source = new IPEndPoint(IPAddress.Any, 0);
                    int dataLen = -1;
                    try
                    {
                        dataLen = socket.ReceiveFrom(ReadBuffer, ref source);
                        ReadPacket.FromBytes(ReadBuffer, 0, dataLen);
                    }
                    catch { }

                    if (dataLen <= 0)
                    {
                        continue;
                    }
                    
                    ReadPacket.StartMessageRead();
                    ReadPacket.TryGetNextMessage(out var message);
                    var response = (ConnectionResponseCode)BitConverter.ToInt32(message);

                    if (response == ConnectionResponseCode.Accepted)
                    {
                        if (Logger.includes.connectionAttempts)
                        {
                            Logger.Write("Connection request to " + Address.ToString() + " accepted at " + Timestamp.NowString);
                        }

                        _acceptedRequest = true;
                        _tcpClient.Connect(_tcpEndPoint);
                        _latency = (int)(Timestamp.Now - ReadPacket.header.timestamp);
                    }
                    else
                    {
                        if (Logger.includes.connectionAttempts)
                        {
                            Logger.Write("Connection request to " + Address.ToString() + " rejected at " + Timestamp.NowString + " with response code of: " + response.ToString());
                        }

                        _remainingRequests = 0;

                        AddIncoming(new IncomingMessage{
                            caller = ClientId.None,
                            callee = LocalId,
                            rpcId = new RpcId(RpcId.ConnectionRejectedId),
                            protocol = Protocol.Udp,
                            perms = RpcPerms.AuthorityToClients,
                            args = new object[]{response}
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Reads any data currently on the socket. Putting new messages in the queue.
        /// Blocks infinitely while waiting for the server to initially assign the buffer a ClientId.
        /// </summary>
        public override void Recv()
        {
            if (!_acceptedRequest && _remainingRequests > 0)
            {
                Connect();
                if (_remainingRequests <= 0)
                    Disconnect();
                return;
            }

            if (!_tcpClient.Connected)
            {
                if (IsActive)
                    Disconnect();
                return;
            }

            _readList.Clear();
            _readList.Add(_tcpClient);
            _readList.Add(_udpClient.Socket);
            Socket.Select(_readList, null, null, IsReady ? 0 : -1);

            _pingRequests.ClearTimeouts(PingTimeout);

            foreach (var socket in _readList)
            {
                if (socket == _tcpClient)
                {
                    Array.Clear(ReadBuffer, 0, ReadBuffer.Length);
                    int dataRemaining = -1;
                    int dataLen = -1;

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

                        if (dataLen <= 0)
                        {
                            Disconnect();
                            return;
                        }

                        _latency = (int)(Timestamp.Now - ReadPacket.header.timestamp);

                        if (ReadPacket.header.connectionConfirmation)
                            continue;

                        if (Logger.includes.tcpPostTransform)
                        {
                            var packetStr = new StringBuilder($"RECEIVED: mutated Post-Transform TCP packet from server by {LocalId}:\n");
                            PacketToString(ReadPacket, packetStr);
                            Logger.Write(packetStr.ToString());
                        }

                        ApplyReadSteps(ReadPacket);

                        if (Logger.includes.tcpPreTransform)
                        {
                            var packetStr = new StringBuilder($"RECEIVED: original Pre-Transform TCP packet from server by {LocalId}:\n");
                            PacketToString(ReadPacket, packetStr);
                            Logger.Write(packetStr.ToString());
                        }
                        
                        ReadPacket.StartMessageRead();
                        while (ReadPacket.TryGetNextMessage(out var bytes))
                        {
                            try
                            {
                                if (TryClientMessageDecode(bytes, out var rpcId))
                                {
                                    HandleClientConnectionMessage(rpcId, bytes.Slice(RpcId.MaxByteLength));
                                }
                                else if (TryPingRequestDecode(bytes, out var request))
                                {
                                    HandlePingRequest(request, Protocol.Tcp);
                                }
                                else
                                {
                                    Decode(ClientId.None, bytes, Protocol.Tcp);
                                }
                            }
                            catch (Exception e)
                            {
                                if (Logger.includes.exceptions)
                                    Logger.Write($"FAILED to decode TCP message '{BitConverter.ToString(bytes.ToArray())}'. Exception thrown:\n{e}");
                            }
                        }
                    } while (dataRemaining > 0);
                }
                else if (socket == _udpClient.Socket)
                {
                    while (_udpClient.Available > 0)
                    {
                        Array.Clear(ReadBuffer, 0, ReadBuffer.Length);

                        IPEndPoint source = new IPEndPoint(IPAddress.Any, 0);
                        int dataLen = -1;
                        var result = RudpResult.Failed;
                        try
                        {
                            result = _udpClient.ReceiveFrom(ReadBuffer, ref source, out dataLen);
                        }
                        catch { }

                        if (dataLen <= 0)
                            break;
                        
                        if (result == RudpResult.PingRequest)
                        {
                            ReadPacket.FromBytes(ReadBuffer, 0, dataLen);
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
                                        Logger.Write($"FAILED to handle UDP ping request '{BitConverter.ToString(bytes.ToArray())}'. Exception thrown:\n{e}");
                                }
                            }
                        }
                    }
                }
            }

            _udpClient.RequestMissingPackets();

            while (_udpClient.TryGetNextPacket(out var packet, out var source))
            {
                ReadPacket.Clear();
                ReadPacket.FromBytes(packet, 0, packet.Length);

                if (Logger.includes.udpPostTransform)
                {
                    var packetStr = new StringBuilder($"RECEIVED: mutated Post-Transform UDP packet from server by {LocalId}:\n");
                    PacketToString(ReadPacket, packetStr);
                    Logger.Write(packetStr.ToString());
                }

                ApplyReadSteps(ReadPacket);

                if (Logger.includes.udpPreTransform)
                {
                    var packetStr = new StringBuilder($"RECEIVED: original Pre-Transform UDP packet from server by {LocalId}:\n");
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
                            Decode(ClientId.None, bytes, Protocol.Udp);
                        }
                    }
                    catch (Exception e)
                    {
                        if (Logger.includes.exceptions)
                            Logger.Write($"FAILED to decode UDP message '{BitConverter.ToString(bytes.ToArray())}'. Exception thrown:\n{e}");
                    }
                }
            }
        }

        // handle connections and disconnections immediately, 
        // message order only preserved for main simulation.
        private void HandleClientConnectionMessage(RpcId messageType, ReadOnlySpan<byte> bytes)
        {
            switch (messageType.Id)
            {
                case RpcId.ClientConnectedId:
                    var id = new ClientId(bytes);
                    _clients.Add(id);
                    AddClientConnectedMessage(id);
                    break;
                case RpcId.LocalClientConnectedId:
                    var assignment = new ClientIdAssignment(bytes);
                    _clients.Add(assignment.assignedId);
                    LocalId = assignment.assignedId;
                    Authority = assignment.authorityId;
                    _hash = assignment.assignedHash;
                    MaxClients = assignment.maxClients;
                    IsReady = true;
                    AddReadyMessage(LocalId);
                    break;
                case RpcId.ClientDisconnectedId:
                    id = new ClientId(bytes);
                    _clients.Remove(id);
                    AddClientDisconnectedMessage(id);
                    break;
                case RpcId.HostMigrationId:
                    Authority = new ClientId(bytes);
                    AddHostMigrationMessage(Authority);
                    break;
                default: break;
            }
        }

        private void HandlePingRequest(PingRequest request, Protocol protocol = Protocol.Udp)
        {
            if (request.Target == LocalId)
            {
                ReadPacket.Clear();
                ReadPacket.header.timestamp = Timestamp.Now;
                ReadPacket.header.sender = LocalId.Id;
                ReadPacket.header.hash = _hash;
                ReadPacket.header.pingRequest = true;
                PingResponse(request, ReadPacket);
                if (protocol == Protocol.Udp)
                    _udpClient.Socket.SendTo(ReadPacket.GetPacket().ToArray(), _udpEndPoint);
                else
                    _tcpClient.Send(ReadPacket.GetPacket());
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
                        caller = ClientId.None, 
                        callee = LocalId, 
                        rpcId = new RpcId(RpcId.PingRequestId), 
                        target = NetworkId.None, 
                        protocol = protocol, 
                        perms = RpcPerms.AnyToAll, 
                        args = new object[]{original}
                    });
                }
            }
        }

        /// <summary>
        /// Write current outgoing buffer to the server socket.
        /// Buffer is cleared after writing.
        /// </summary>
        public override void Send()
        {
            while (TryGetNextOutgoing(out var message))
            {
                if (HandleClientEvent(message))
                    continue;
                
                if (message.rpcId == RpcId.PingRequestId && TryPingRequestDecode(message.bytes, out var request))
                {
                    var original = _pingRequests.Find(request);
                    original.PingSent();
                    PingRequestEncode(message.bytes, original);

                    ReadPacket.Clear();
                    ReadPacket.header.timestamp = Timestamp.Now;
                    ReadPacket.header.sender = LocalId.Id;
                    ReadPacket.header.hash = _hash;
                    ReadPacket.header.pingRequest = true;
                    AddToPacket(message, ReadPacket);

                    if (message.protocol == Protocol.Udp)
                        _udpClient.Socket.SendTo(ReadPacket.GetPacket().ToArray(), _udpEndPoint);
                    else
                        _tcpClient.Send(ReadPacket.GetPacket());
                    continue;
                }
                
                Packet p = message.protocol == Protocol.Tcp ? _tcpPacket : _udpPacket;
                AddToPacket(message, p);
            }

            while (!_tcpPacket.IsEmpty)
            {
                _tcpPacket.header.timestamp = Timestamp.Now;
                _tcpPacket.header.sender = LocalId.Id;
                _tcpPacket.header.hash = _hash;

                if (Logger.includes.tcpPreTransform)
                {
                    var packetStr = new StringBuilder($"SENDING: Pre-Transform TCP packet to server from {LocalId}:\n");
                    PacketToString(_tcpPacket, packetStr);
                    Logger.Write(packetStr.ToString());
                }

                ApplySendSteps(_tcpPacket);

                if (Logger.includes.tcpPostTransform)
                {
                    var packetStr = new StringBuilder($"SENDING: Post-Transform TCP packet to server from {LocalId}:\n");
                    PacketToString(_tcpPacket, packetStr);
                    Logger.Write(packetStr.ToString());
                }

                var bytes = _tcpPacket.GetPacket();
                _tcpClient.Send(bytes);
                _tcpPacket.Reset();
            }
            HasClientEvent = false;

            while (!_udpPacket.IsEmpty)
            {
                _udpPacket.header.timestamp = Timestamp.Now;
                _udpPacket.header.sender = LocalId.Id;
                _udpPacket.header.hash = _hash;

                if (Logger.includes.udpPreTransform)
                {
                    var packetStr = new StringBuilder($"SENDING: Pre-Transform UDP packet to server from {LocalId}:\n");
                    PacketToString(_udpPacket, packetStr);
                    Logger.Write(packetStr.ToString());
                }

                ApplySendSteps(_udpPacket);

                if (Logger.includes.udpPostTransform)
                {
                    var packetStr = new StringBuilder($"SENDING: Post-Transform UDP packet to server from {LocalId}:\n");
                    PacketToString(_udpPacket, packetStr);
                    Logger.Write(packetStr.ToString());
                }

                var bytes = _udpPacket.GetPacket();
                _udpClient.SendTo(bytes.ToArray(), _udpEndPoint);
                _udpPacket.Reset();
            }
        }

        /// <summary>
        /// Disconnect the client from the server.
        /// Invokes <c>OnClientDisconnected</c> with the local ClientId.
        /// </summary>
        public override void Disconnect()
        {
            if (IsActive)
                AddClientDisconnectedMessage(LocalId);
            IsActive = false;
            IsReady = false;
            if (_tcpClient.Connected)
                _tcpClient.Close();
            _udpClient.Close();
        }

        /// <summary>
        /// If this client is the authority, tell the server to disconnect the given client.
        /// </summary>
        public override void Disconnect(ClientId id)
        {
            if (LocalId != Authority)
                throw new InvalidOperationException("Only the authority can disconnect other clients.");
            var span = _tcpPacket.GetSpan(ClientMessageLength);
            ClientDisconnectEncode(span, id);
            HasClientEvent = true;
        }

        public override void MigrateHost(ClientId newHost)
        {
            if (LocalId != Authority)
                throw new InvalidOperationException("Only the authority can migrate the host role.");
            if (!_clients.Contains(newHost))
                return;
            var span = _tcpPacket.GetSpan(ClientMessageLength);
            HostMigrationEncode(span, newHost);
            HasClientEvent = true;
        }
    }
}