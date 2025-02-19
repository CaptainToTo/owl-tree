
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
            _udpServer = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _udpServer.Bind(udpEndPoint);
            ServerUdpPort = ((IPEndPoint)_udpServer.LocalEndPoint).Port;
            _readList.Add(_udpServer);

            _clientData = new ClientDataList(BufferSize, DateTimeOffset.UtcNow.Millisecond);
            _whitelist = whitelist;

            MaxClients = maxClients == -1 ? int.MaxValue : maxClients;
            _requests = new(MaxClients, requestTimeout);
            LocalId = ClientId.None;
            Authority = ClientId.None;
            IsReady = true;
            OnReady?.Invoke(LocalId);
        }

        public override int LocalTcpPort() => ServerTcpPort;

        public override int LocalUdpPort() => ServerUdpPort;

        // server state
        private Socket _tcpServer;
        private Socket _udpServer;
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
            _readList.Add(_udpServer);
            foreach (var data in _clientData)
                _readList.Add(data.tcpSocket);
            
            Socket.Select(_readList, null, null, 0);

            _requests.ClearTimeouts();

            foreach (var socket in _readList)
            {
                // new client connects
                if (socket == _tcpServer)
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

                    OnClientConnected?.Invoke(clientData.id);

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
                else if (socket == _udpServer) // receive client udp messages
                {
                    while (_udpServer.Available > 0)
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
                            break;
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

                            ReadPacket.StartMessageRead();
                            if (ReadPacket.TryGetNextMessage(out var bytes))
                            {
                                var rpcId = ServerMessageDecode(bytes, out var request);
                                if (
                                    rpcId == RpcId.ConnectionRequestId && 
                                    request.appId == ApplicationId && request.sessionId == SessionId && !request.isHost &&
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
                                _udpServer.SendTo(responsePacket.ToArray(), source);
                            }

                            if (Logger.includes.connectionAttempts)
                            {
                                Logger.Write("Connection attempt from " + ((IPEndPoint)source).Address.ToString() + " (udp port: " + ((IPEndPoint)source).Port + ") " + (accepted ? "accepted, awaiting TCP handshake..." : "rejected."));
                            }
                            continue;
                        }
                        else if (client.hash != ReadPacket.header.hash)
                        {
                            if (Logger.includes.exceptions)
                                Logger.Write($"Incorrect hash received in UDP packet from client {client.id}. Got {ReadPacket.header.hash}, but expected {client.hash}. Ignoring packet.");
                            continue;
                        }

                        if (Logger.includes.udpPreTransform)
                        {
                            var packetStr = new StringBuilder($"RECEIVED: Pre-Transform UDP packet from {client.id}:\n");
                            PacketToString(ReadPacket, packetStr);
                            Logger.Write(packetStr.ToString());
                        }

                        ApplyReadSteps(ReadPacket);

                        if (Logger.includes.udpPostTransform)
                        {
                            var packetStr = new StringBuilder($"RECEIVED: Post-Transform UDP packet from {client.id}:\n");
                            PacketToString(ReadPacket, packetStr);
                            Logger.Write(packetStr.ToString());
                        }

                        ReadPacket.StartMessageRead();
                        while (ReadPacket.TryGetNextMessage(out var bytes))
                        {
                            try
                            {
                                if (TryDecode(client.id, bytes, out var message))
                                {
                                    _incoming.Enqueue(message);
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

                        // disconnect if receive fails
                        if (dataLen <= 0)
                        {
                            Disconnect(_clientData.Find(socket));
                            break;
                        }

                        if (client == ClientData.None)
                        {
                            client = _clientData.Find(socket);

                            if (client.hash != ReadPacket.header.hash)
                            {
                                if (Logger.includes.exceptions)
                                    Logger.Write($"Incorrect hash received in TCP packet from client {client.id}. Got {ReadPacket.header.hash}, but expected {client.hash}. Ignoring packet.");
                                continue;
                            }
                        }

                        if (Logger.includes.tcpPreTransform)
                        {
                            var packetStr = new StringBuilder($"RECEIVED: Pre-Transform TCP packet from {client.id}:\n");
                            PacketToString(ReadPacket, packetStr);
                            Logger.Write(packetStr.ToString());
                        }

                        ApplyReadSteps(ReadPacket);

                        if (Logger.includes.tcpPostTransform)
                        {
                            var packetStr = new StringBuilder($"RECEIVED: Post-Transform TCP packet from {client.id}:\n");
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
                                    HandlePingRequest(request);
                                }
                                else if (TryDecode(client.id, bytes, out var message))
                                {
                                    _incoming.Enqueue(message);
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
                    _incoming.Enqueue(new Message(
                        ClientId.None, 
                        LocalId, 
                        new RpcId(RpcId.PingRequestId), 
                        NetworkId.None, 
                        Protocol.Tcp, 
                        RpcPerms.AnyToAll,
                        new object[]{original}));
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
                            if (message.caller == client.id) continue;
                            Encode(message, client.tcpPacket);
                        }
                    }
                    else
                    {
                        foreach (var client in _clientData)
                        {
                            if (message.caller == client.id) continue;
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

                    client.tcpSocket.Send(bytes);
                    client.tcpPacket.Reset();
                }

                if (!client.udpPacket.IsEmpty)
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

                    _udpServer.SendTo(bytes.ToArray(), client.udpEndPoint);
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
            var ids = _clientData.GetIds();
            foreach (var id in ids)
            {
                Disconnect(id);
            }
            _tcpServer.Close();
            _udpServer.Close();
            IsReady = false;
            IsActive = false;
            OnClientDisconnected?.Invoke(LocalId);
        }


        /// <summary>
        /// Disconnect a client from the server.
        /// Invokes <c>OnClientDisconnected</c>.
        /// </summary>
        public override void Disconnect(ClientId id)
        {
            var client = _clientData.Find(id);
            if (client != ClientData.None)
                Disconnect(client);
        }

        private void Disconnect(ClientData client)
        {
            _clientData.Remove(client);
            client.tcpSocket.Close();
            OnClientDisconnected?.Invoke(client.id);

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