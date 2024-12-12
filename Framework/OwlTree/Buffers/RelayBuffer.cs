
using System;
using System.Collections.Generic;
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
        public RelayBuffer(Args args, int maxClients, long requestTimeout, string hostAddr) : base(args)
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

            if (hostAddr != null)
                _hostAddr = IPAddress.Parse(hostAddr);

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
        private ClientDataList _clientData = new();
        private ConnectionRequestList _requests;

        private IPAddress _hostAddr = null;

        public override void Read()
        {
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

                    var clientData = new ClientData() {
                        id = ClientId.New(), 
                        tcpPacket = new Packet(BufferSize), 
                        tcpSocket = tcpClient,
                        udpPacket = new Packet(BufferSize, true),
                        udpEndPoint = udpEndPoint
                    };
                    clientData.tcpPacket.header.owlTreeVer = OwlTreeVersion;
                    clientData.tcpPacket.header.appVer = AppVersion;
                    clientData.udpPacket.header.owlTreeVer = OwlTreeVersion;
                    clientData.udpPacket.header.appVer = AppVersion;

                    _clientData.Add(clientData);

                    if (Logger.includes.connectionAttempts)
                    {
                        Logger.Write($"TCP handshake made with {((IPEndPoint)tcpClient.RemoteEndPoint).Address} (tcp port: {((IPEndPoint)tcpClient.RemoteEndPoint).Port}) (udp port: {udpPort}). Assigned: {clientData.id}");
                    }

                    if (_hostAddr == null || _hostAddr.Equals(((IPEndPoint)tcpClient.RemoteEndPoint).Address))
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
                    LocalClientConnectEncode(span, new ClientIdAssignment(clientData.id, Authority));

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
                            ReadPacket.header.target = 0;
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
                        if (TryDecode(client.id, bytes, out var message))
                        {
                            _incoming.Enqueue(message);
                        }
                    }
                }
            }
        }

        public override void Send()
        {
            throw new System.NotImplementedException();
        }

        public override void Disconnect()
        {
            throw new System.NotImplementedException();
        }

        public override void Disconnect(ClientId id)
        {
            throw new System.NotImplementedException();
        }
    }
}