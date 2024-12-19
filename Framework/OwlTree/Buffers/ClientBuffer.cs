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
            _tcpEndPoint = new IPEndPoint(Address, TcpPort);

            _udpClient = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _udpClient.Bind(new IPEndPoint(IPAddress.Any, 0));

            _udpEndPoint = new IPEndPoint(Address, ServerUdpPort);

            _readList.Add(_tcpClient);
            _readList.Add(_udpClient);

            _tcpPacket = new Packet(BufferSize);
            _tcpPacket.header.owlTreeVer = OwlTreeVersion;
            _tcpPacket.header.appVer = AppVersion;
            _udpPacket = new Packet(BufferSize, true);
            _udpPacket.header.owlTreeVer = OwlTreeVersion;
            _udpPacket.header.appVer = AppVersion;

            _requestRate = requestRate;
            _remainingRequests = requestLimit;
            _requestAsHost = requestAsHost;
        }

        // client state
        private Socket _tcpClient;
        private Socket _udpClient;
        private IPEndPoint _tcpEndPoint;
        private IPEndPoint _udpEndPoint;
        private List<Socket> _readList = new List<Socket>();
        private List<ClientId> _clients = new List<ClientId>();

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

            if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _lastRequest > _requestRate)
            {
                var idBytes = _udpPacket.GetSpan(ConnectionRequestLength);
                ConnectionRequestEncode(idBytes, new ConnectionRequest(ApplicationId, _requestAsHost));
                _udpClient.SendTo(_udpPacket.GetPacket().ToArray(), _udpEndPoint);
                _udpPacket.Clear();
                _lastRequest = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _remainingRequests--;

                if (Logger.includes.connectionAttempts)
                {
                    Logger.Write("Connection request made to " + Address.ToString() + " at " + DateTime.UtcNow);
                }
            }

            _readList.Clear();
            _readList.Add(_udpClient);
            Socket.Select(_readList, null, null, _requestRate);

            foreach (var socket in _readList)
            {
                if (socket == _udpClient)
                {
                    Array.Clear(ReadBuffer, 0, ReadBuffer.Length);
                    ReadPacket.Clear();

                    EndPoint source = new IPEndPoint(IPAddress.Any, 0);
                    int dataLen = -1;
                    try
                    {
                        dataLen = socket.ReceiveFrom(ReadBuffer, ref source);
                        ReadPacket.FromBytes(ReadBuffer, 0);
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
                            Logger.Write("Connection request to " + Address.ToString() + " accepted at " + DateTime.UtcNow);
                        }

                        _acceptedRequest = true;
                        _tcpClient.Connect(_tcpEndPoint);
                    }
                    else
                    {
                        if (Logger.includes.connectionAttempts)
                        {
                            Logger.Write("Connection request to " + Address.ToString() + " rejected at " + DateTime.UtcNow + " with response code of: " + response.ToString());
                        }

                        _remainingRequests = 0;
                    }
                }
            }
        }

        /// <summary>
        /// Reads any data currently on the socket. Putting new messages in the queue.
        /// Blocks infinitely while waiting for the server to initially assign the buffer a ClientId.
        /// </summary>
        public override void Read()
        {
            if (!_acceptedRequest && _remainingRequests > 0)
            {
                Connect();
                if (_remainingRequests <= 0)
                    Disconnect();
                return;
            }

            if (!_tcpClient.Connected)
                return;

            _readList.Clear();
            _readList.Add(_tcpClient);
            _readList.Add(_udpClient);
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
                                dataRemaining -= ReadPacket.FromBytes(ReadBuffer, dataLen - dataRemaining);
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

                        if (Logger.includes.tcpPreTransform)
                        {
                            var packetStr = new StringBuilder($"RECEIVED: Pre-Transform TCP packet from server at {DateTime.UtcNow}:\n");
                            PacketToString(ReadPacket, packetStr);
                            Logger.Write(packetStr.ToString());
                        }

                        ApplyReadSteps(ReadPacket);

                        if (Logger.includes.tcpPostTransform)
                        {
                            var packetStr = new StringBuilder($"RECEIVED: Post-Transform TCP packet from server at {DateTime.UtcNow}:\n");
                            PacketToString(ReadPacket, packetStr);
                            Logger.Write(packetStr.ToString());
                        }
                        
                        ReadPacket.StartMessageRead();
                        while (ReadPacket.TryGetNextMessage(out var bytes))
                        {
                            if (TryClientMessageDecode(bytes, out var rpcId))
                            {
                                HandleClientConnectionMessage(rpcId, bytes.Slice(RpcId.MaxLength()));
                            }
                            else if (TryPingRequestDecode(bytes, out var request))
                            {
                                HandlePingRequest(request);
                            }
                            else if (TryDecode(ClientId.None, bytes, out var message))
                            {
                                _incoming.Enqueue(message);
                            }
                        }
                    } while (dataRemaining > 0);
                }
                else if (socket == _udpClient)
                {
                    Array.Clear(ReadBuffer, 0, ReadBuffer.Length);
                    ReadPacket.Clear();

                    EndPoint source = new IPEndPoint(IPAddress.Any, 0);
                    int dataLen = -1;
                    try
                    {
                        dataLen = socket.ReceiveFrom(ReadBuffer, ref source);
                        ReadPacket.FromBytes(ReadBuffer, 0);
                    }
                    catch { }

                    if (dataLen <= 0)
                    {
                        continue;
                    }

                    if (Logger.includes.udpPreTransform)
                    {
                        var packetStr = new StringBuilder($"RECEIVED: Pre-Transform UDP packet from server at {DateTime.UtcNow}:\n");
                        PacketToString(ReadPacket, packetStr);
                        Logger.Write(packetStr.ToString());
                    }

                    ApplyReadSteps(ReadPacket);

                    if (Logger.includes.udpPostTransform)
                    {
                        var packetStr = new StringBuilder($"RECEIVED: Post-Transform UDP packet from server at {DateTime.UtcNow}:\n");
                        PacketToString(ReadPacket, packetStr);
                        Logger.Write(packetStr.ToString());
                    }

                    ReadPacket.StartMessageRead();
                    while (ReadPacket.TryGetNextMessage(out var bytes))
                    {
                        if (TryDecode(ClientId.None, bytes, out var message))
                        {
                            _incoming.Enqueue(message);
                        }
                    }
                }
            }
        }

        // handle connections and disconnections immediately, 
        // they do not preserve the message execution order.
        private void HandleClientConnectionMessage(RpcId messageType, ReadOnlySpan<byte> bytes)
        {
            switch (messageType.Id)
            {
                case RpcId.CLIENT_CONNECTED_MESSAGE_ID:
                    var id = new ClientId(bytes);
                    _clients.Add(id);
                    OnClientConnected?.Invoke(id);
                    break;
                case RpcId.LOCAL_CLIENT_CONNECTED_MESSAGE_ID:
                    var assignment = new ClientIdAssignment(bytes);
                    _clients.Add(assignment.assignedId);
                    LocalId = assignment.assignedId;
                    Authority = assignment.authorityId;
                    _hash = assignment.assignedHash;
                    IsReady = true;
                    OnReady?.Invoke(LocalId);
                    break;
                case RpcId.CLIENT_DISCONNECTED_MESSAGE_ID:
                    id = new ClientId(bytes);
                    _clients.Remove(id);
                    OnClientDisconnected?.Invoke(id);
                    break;
                case RpcId.HOST_MIGRATION:
                    Authority = new ClientId(bytes);
                    OnHostMigration?.Invoke(Authority);
                    break;
                default: break;
            }
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
        }

        /// <summary>
        /// Write current outgoing buffer to the server socket.
        /// Buffer is cleared after writing.
        /// </summary>
        public override void Send()
        {
            while (_outgoing.TryDequeue(out var message))
            {
                if (message.protocol == Protocol.Tcp)
                    Encode(message, _tcpPacket);
                else
                    Encode(message, _udpPacket);
            }

            if (!_tcpPacket.IsEmpty)
            {
                _tcpPacket.header.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _tcpPacket.header.sender = LocalId.Id;
                _tcpPacket.header.hash = _hash;

                if (Logger.includes.tcpPreTransform)
                {
                    var packetStr = new StringBuilder($"SENDING: Pre-Transform TCP packet to server at {DateTime.UtcNow}:\n");
                    PacketToString(_tcpPacket, packetStr);
                    Logger.Write(packetStr.ToString());
                }

                ApplySendSteps(_tcpPacket);

                if (Logger.includes.tcpPostTransform)
                {
                    var packetStr = new StringBuilder($"SENDING: Post-Transform TCP packet to server at {DateTime.UtcNow}:\n");
                    PacketToString(_tcpPacket, packetStr);
                    Logger.Write(packetStr.ToString());
                }

                var bytes = _tcpPacket.GetPacket();
                _tcpClient.Send(bytes);
                _tcpPacket.Reset();
            }
            HasClientEvent = false;

            if (!_udpPacket.IsEmpty)
            {
                _udpPacket.header.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _udpPacket.header.sender = LocalId.Id;
                _udpPacket.header.hash = _hash;

                if (Logger.includes.udpPreTransform)
                {
                    var packetStr = new StringBuilder($"SENDING: Pre-Transform UDP packet to server at {DateTime.UtcNow}:\n");
                    PacketToString(_udpPacket, packetStr);
                    Logger.Write(packetStr.ToString());
                }

                ApplySendSteps(_udpPacket);

                if (Logger.includes.udpPostTransform)
                {
                    var packetStr = new StringBuilder($"SENDING: Post-Transform UDP packet to server at {DateTime.UtcNow}:\n");
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
            IsActive = false;
            IsReady = false;
            _tcpClient.Close();
            _udpClient.Close();
            OnClientDisconnected?.Invoke(LocalId);
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