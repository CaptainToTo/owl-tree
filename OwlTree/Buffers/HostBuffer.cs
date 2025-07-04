using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace OwlTree
{
    internal abstract class HostBuffer : NetworkBuffer
    {
        protected HostBuffer(Args args, int maxClients, long requestTimeout, IPAddress[] whitelist) : base(args)
        {
            IPEndPoint tpcEndPoint = new IPEndPoint(IPAddress.Any, ServerTcpPort);
            TcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            TcpSocket.Bind(tpcEndPoint);
            TcpSocket.Listen(maxClients);
            ServerTcpPort = ((IPEndPoint)TcpSocket.LocalEndPoint).Port;
            ReadList.Add(TcpSocket);

            IPEndPoint udpEndPoint = new IPEndPoint(IPAddress.Any, ServerUdpPort);
            UdpSocket = new RudpServerSocket(udpEndPoint);
            ServerUdpPort = UdpSocket.Port;
            ReadList.Add(UdpSocket.Socket);

            ClientData = new ClientDataList(BufferSize, Timestamp.Millisecond);
            _whitelist = whitelist;

            MaxClients = maxClients == -1 ? int.MaxValue : maxClients;
            ConnectRequests = new(MaxClients, requestTimeout);
            LocalId = ClientId.None;
            Authority = ClientId.None;
        }

        ~HostBuffer()
        {
            if (TcpSocket.Connected)
                Disconnect();
        }

        public override int LocalTcpPort() => ServerTcpPort;

        public override int LocalUdpPort() => ServerUdpPort;

        public override int Latency() => ClientData.FindWorstLatency()?.latency ?? 0;

        // server state
        protected Socket TcpSocket;
        protected RudpServerSocket UdpSocket;
        protected List<Socket> ReadList = new();
        protected ClientDataList ClientData;
        protected ConnectionRequestList ConnectRequests;

        private IPAddress[] _whitelist = null;

        protected bool HasWhitelist => _whitelist != null && _whitelist.Length > 0;

        protected bool IsOnWhitelist(IPAddress addr)
        {
            if (!HasWhitelist) return false;
            foreach (var a in _whitelist)
                if (a.Equals(addr)) return true;
            return false;
        }

        protected void ResetReadList()
        {
            ReadList.Clear();
            ReadList.Add(TcpSocket);
            ReadList.Add(UdpSocket.Socket);
            foreach (var data in ClientData)
                ReadList.Add(data.tcpSocket);

            Socket.Select(ReadList, null, null, 0);

            ConnectRequests.ClearTimeouts();

            PingRequests.ClearTimeouts(PingTimeout);
        }

        protected ClientData TryAddClient()
        {
            var tcpClient = TcpSocket.Accept();

            // reject connections that aren't from verified app instances
            if (!ConnectRequests.TryGet((IPEndPoint)tcpClient.RemoteEndPoint, out var udpPort, out var timestamp))
            {
                tcpClient.Close();
                return null;
            }

            IPEndPoint udpEndPoint = new IPEndPoint(((IPEndPoint)tcpClient.RemoteEndPoint).Address, udpPort);

            var clientData = ClientData.Add(tcpClient, udpEndPoint);
            clientData.tcpPacket.header.owlTreeVer = OwlTreeVersion;
            clientData.tcpPacket.header.appVer = AppVersion;
            clientData.udpPacket.header.owlTreeVer = OwlTreeVersion;
            clientData.udpPacket.header.appVer = AppVersion;
            clientData.latency = (int)(Timestamp.Now - timestamp);
            UdpSocket.AddEndpoint(udpEndPoint);

            if (Logger.includes.connectionAttempts)
                Logger.Write($"TCP handshake made with {((IPEndPoint)tcpClient.RemoteEndPoint).Address} (tcp port: {((IPEndPoint)tcpClient.RemoteEndPoint).Port}) (udp port: {udpPort}). Assigned: {clientData.id}");

            AddClientConnectedMessage(clientData.id);

            return clientData;
        }

        protected void SendNewClient(ClientData clientData)
        {
            // send new client their id
            var span = clientData.tcpPacket.GetSpan(Encoder.LocalClientConnectLength);
            Encoder.LocalClientConnectEncode(span, new ClientIdAssignment(
                clientData.id, Authority, clientData.hash, MaxClients, Migratable, ShutdownWhenEmpty));

            foreach (var otherClient in ClientData)
            {
                if (otherClient.id == clientData.id) continue;

                // notify clients of a new client in the next send
                span = otherClient.tcpPacket.GetSpan(Encoder.ClientMessageLength);
                Encoder.ClientConnectEncode(span, clientData.id);

                // add existing clients to new client
                span = clientData.tcpPacket.GetSpan(Encoder.ClientMessageLength);
                Encoder.ClientConnectEncode(span, otherClient.id);
            }
            HasClientEvent = true;

            clientData.tcpPacket.header.timestamp = Timestamp.Now;
            ApplySendSteps(clientData.tcpPacket);
            var bytes = clientData.tcpPacket.GetPacket();
            clientData.tcpSocket.Send(bytes);
            clientData.tcpPacket.Reset();
        }

        protected (RudpResult result, IPEndPoint source) RecvUdpPacket()
        {
            Array.Clear(ReadBuffer, 0, ReadBuffer.Length);

            IPEndPoint source = new IPEndPoint(IPAddress.Any, 0);
            int dataLen = -1;
            RudpResult result = RudpResult.Failed;
            try
            {
                result = UdpSocket.ReceiveFrom(ReadBuffer, ref source, out dataLen);
                ReadPacket.FromBytes(ReadBuffer, 0, dataLen);

                if (ReadPacket.header.appVer < MinAppVersion || ReadPacket.header.owlTreeVer < MinOwlTreeVersion)
                    throw new InvalidOperationException("Cannot accept packets from outdated OwlTree or app versions.");
            }
            catch (Exception e)
            {
                if (Logger.includes.exceptions)
                    Logger.WriteError("Failed to receive UDP packet.", e);
            }

            if (dataLen <= 0)
                return (RudpResult.Failed, null);
            return (result, source);
        }

        protected ClientData FindClientData(Socket socket)
        {
            var client = ClientData.Find(socket);
            if (!socket.Connected)
            {
                Disconnect(client);
                return null;
            }
            return client;
        }

        protected (int dataRemaining, int dataLen) RecvTcpPacket(Socket socket, ClientData client, int dataRemaining, int dataLen)
        {
            ReadPacket.Clear();

            int iters = 0;
            do
            {
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
                catch (Exception e)
                {
                    if (Logger.includes.exceptions)
                        Logger.WriteError($"Failed to receive TCP packet from {((IPEndPoint)socket.RemoteEndPoint).Address} ({client.id})", e);

                    dataLen = -1;
                    break;
                }
            } while (ReadPacket.Incomplete && iters < 10);

            if (ReadPacket.header.appVer < MinAppVersion || ReadPacket.header.owlTreeVer < MinOwlTreeVersion)
                dataLen = -1;

            if (client.hash != ReadPacket.header.hash)
            {
                if (Logger.includes.exceptions)
                    Logger.Write($"Incorrect hash received in TCP packet from client {client.id}. Got {ReadPacket.header.hash}, but expected {client.hash}. Ignoring packet. Client has failed {client.failed} times, will disconnect at 10.");
                client.failed++;
                if (client.failed > 10)
                    Disconnect(client);
                return (-1, -1);
            }
            client.failed = 0;

            var time = Timestamp.Now;
            client.latency = (int)(time - ReadPacket.header.timestamp);
            client.lastConfirmed = time;

            // disconnect if receive fails
            if (dataLen <= 0)
            {
                Disconnect(client);
                return (-1, -1); // break
            }

            if (Logger.includes.tcpPostTransform)
                Logger.WriteRecv($"mutated Post-Transform TCP packet from {client.id}:", ReadPacket);

            ApplyRecvSteps(ReadPacket);

            if (Logger.includes.tcpPreTransform)
                Logger.WriteRecv($"original Pre-Transform TCP packet from {client.id}:", ReadPacket);

            return (dataRemaining, dataLen);
        }

        protected void ParsePingRequest(IPEndPoint source)
        {
            var client = ClientData.Find(source);

            if (client == null)
            {
                if (Logger.includes.exceptions)
                    Logger.Write($"Ping request received from an unknown client. Ignoring packet.");
                return;
            }

            if (client.hash != ReadPacket.header.hash)
            {
                if (Logger.includes.exceptions)
                    Logger.Write($"Incorrect hash received in UDP ping request from client {client.id}. Got {ReadPacket.header.hash}, but expected {client.hash}. Ignoring packet.");
                return;
            }

            ReadPacket.StartMessageRead();
            if (ReadPacket.TryGetNextMessage(out var bytes))
            {
                try
                {
                    var rpcId = new RpcId(bytes);

                    if (rpcId.Id == RpcId.PingRequestId && Encoder.TryPingRequestDecode(bytes, out var request))
                        HandlePingRequest(request, Protocol.Udp);
                }
                catch (Exception e)
                {
                    if (Logger.includes.exceptions)
                        Logger.WriteError($"Failed to handle UDP ping request message '{Encoder.ToString(bytes)}' from {client.id}.", e);
                }
            }
        }

        protected void HandlePingRequest(PingRequest request, Protocol protocol = Protocol.Udp)
        {
            if (request.Target == LocalId)
            {
                var data = ClientData.Find(request.Source);

                if (data == null)
                    return;

                ReadPacket.Clear();
                ReadPacket.header.timestamp = Timestamp.Now;
                ReadPacket.header.sender = 0;
                ReadPacket.header.hash = data.hash;
                ReadPacket.header.pingRequest = true;
                PingResponse(request, ReadPacket);
                if (protocol == Protocol.Udp)
                    UdpSocket.Socket.SendTo(ReadPacket.GetPacket().ToArray(), data.udpEndPoint);
                else
                    data.tcpSocket.Send(ReadPacket.GetPacket());
            }
            else if (request.Source == LocalId)
            {
                var original = PingRequests.Find(request);
                if (original != null)
                {
                    original.PingReceivedAt(request.ReceiveTime);
                    original.PingResponded();
                    PingRequests.Remove(original);
                    MessageQueue.AddIncoming(new IncomingMessage
                    {
                        caller = request.Source,
                        callee = request.Target,
                        rpcId = new RpcId(RpcId.PingRequestId),
                        target = NetworkId.None,
                        protocol = protocol,
                        perms = RpcPerms.AnyToAll,
                        args = new object[] { original }
                    });
                }
            }
            else
            {
                var target = ClientData.Find(request.Target);
                var source = ClientData.Find(request.Source);
                if (target == null || source == null)
                    return;

                var packet = request.Received ? source.tcpPacket : target.tcpPacket;
                var span = packet.GetSpan(Encoder.PingRequestLength);
                Encoder.PingRequestEncode(span, request);
                HasClientEvent = true;
            }
        }

        protected ConnectionResponseCode ProcessConnectionRequest(IPEndPoint source)
        {
            if (HasWhitelist && !IsOnWhitelist(source.Address))
                return ConnectionResponseCode.Rejected;

            if (Logger.includes.connectionAttempts)
                Logger.Write("Connection attempt from " + source.Address.ToString() + " (udp port: " + source.Port + ") received:\n" + ReadPacket.ToString());

            ConnectionResponseCode responseCode = ConnectionResponseCode.Accepted;
            ReadPacket.StartMessageRead();
            if (ReadPacket.TryGetNextMessage(out var bytes) &&
                Encoder.TryConnectionRequestDecode(bytes, out var request))
            {
                if (request.appId != ApplicationId)
                    responseCode = ConnectionResponseCode.IncorrectAppId;
                else if (request.sessionId != SessionId)
                    responseCode = ConnectionResponseCode.IncorrectSessionId;
                else if (ClientData.Count >= MaxClients || ConnectRequests.Count >= MaxClients)
                    responseCode = ConnectionResponseCode.SessionFull;
                else if (request.simulationSystem != SimulationSystem || request.tickRate != TickRate)
                    responseCode = ConnectionResponseCode.IncorrectSimulationControl;
                else if (request.isHost)
                    responseCode = ConnectionResponseCode.Rejected;
            }
            else
            {
                responseCode = ConnectionResponseCode.Rejected;
            }

            return responseCode;
        }

        protected void SendConnectionResponse(IPEndPoint target, ConnectionResponseCode responseCode)
        {
            ReadPacket.Clear();
            ReadPacket.header.owlTreeVer = OwlTreeVersion;
            ReadPacket.header.appVer = AppVersion;
            ReadPacket.header.timestamp = Timestamp.Now;
            ReadPacket.header.sender = 0;
            ReadPacket.header.hash = 0;
            var response = ReadPacket.GetSpan(4);
            Encoder.InsertBytes(response, (int)responseCode);
            var responsePacket = ReadPacket.GetPacket();
            UdpSocket.SendTo(responsePacket.ToArray(), target);

            if (Logger.includes.connectionAttempts)
            {
                string resultStr = "accepted, awaiting TCP handshake...";
                switch (responseCode)
                {
                    case ConnectionResponseCode.Accepted:
                        break;
                    case ConnectionResponseCode.SessionFull:
                        resultStr = "rejected, the session is full.";
                        break;
                    case ConnectionResponseCode.IncorrectAppId:
                        resultStr = "rejected, the client gave the incorrect app id.";
                        break;
                    case ConnectionResponseCode.IncorrectSessionId:
                        resultStr = "rejected, the client gave the incorrect session id.";
                        break;
                    case ConnectionResponseCode.IncorrectSimulationControl:
                        resultStr = "rejected, the client is using the incorrect simulation system.";
                        break;
                    case ConnectionResponseCode.HostAlreadyAssigned:
                        resultStr = "rejected, the client tried to claim the host role, but the host is already assigned.";
                        break;
                    case ConnectionResponseCode.Rejected:
                        resultStr = "rejected.";
                        break;
                }
                Logger.Write("Connection attempt from " + target.Address.ToString() + " (udp port: " + target.Port + ") " + resultStr);
            }
        }

        protected void SendPingRequest(OutgoingMessage message, PingRequest request)
        {
            var data = ClientData.Find(message.callee);

            if (data == null)
                return;

            var original = PingRequests.Find(request);
            original.PingSent();
            Encoder.PingRequestEncode(message.bytes, original);

            ReadPacket.Clear();
            ReadPacket.header.timestamp = Timestamp.Now;
            ReadPacket.header.sender = 0;
            ReadPacket.header.hash = data.hash;
            ReadPacket.header.pingRequest = true;
            AddToPacket(message, ReadPacket);


            if (message.protocol == Protocol.Udp)
                UdpSocket.Socket.SendTo(ReadPacket.GetPacket().ToArray(), data.udpEndPoint);
            else
                data.tcpSocket.Send(ReadPacket.GetPacket());
        }

        protected void AddMessageToPackets(OutgoingMessage message)
        {
            if (message.callee != ClientId.None)
            {
                var client = ClientData.Find(message.callee);
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
                    foreach (var client in ClientData)
                    {
                        if (message.caller == client.id) continue;
                        AddToPacket(message, client.tcpPacket);
                    }
                }
                else
                {
                    foreach (var client in ClientData)
                    {
                        if (message.caller == client.id) continue;
                        AddToPacket(message, client.udpPacket);
                    }
                }
            }
        }

        protected void SendTcpPackets(ClientData client)
        {
            while (!client.tcpPacket.IsEmpty)
            {
                client.tcpPacket.header.timestamp = Timestamp.Now;

                if (Logger.includes.tcpPreTransform)
                    Logger.WriteSend($"Pre-Transform TCP packet to {client.id}:", client.tcpPacket);

                ApplySendSteps(client.tcpPacket);
                var bytes = client.tcpPacket.GetPacket();

                if (Logger.includes.tcpPostTransform)
                    Logger.WriteSend($"Post-Transform TCP packet to {client.id}:", client.tcpPacket);

                try
                {
                    client.tcpSocket.Send(bytes);
                    client.lastConfirmed = client.tcpPacket.header.timestamp;
                }
                catch (Exception e)
                {
                    if (Logger.includes.exceptions)
                        Logger.WriteError($"Failed to send TCP packet to {client.id}.", e);
                }
                client.tcpPacket.Reset();
            }
        }

        protected void SendUdpPackets(ClientData client)
        {
            while (!client.udpPacket.IsEmpty)
            {
                client.udpPacket.header.timestamp = Timestamp.Now;

                if (Logger.includes.tcpPreTransform)
                    Logger.WriteSend($"Pre-Transform UDP packet to {client.id}:", client.udpPacket);

                ApplySendSteps(client.udpPacket);
                var bytes = client.udpPacket.GetPacket();

                if (Logger.includes.tcpPostTransform)
                    Logger.WriteSend($"Post-Transform UDP packet to {client.id}:", client.udpPacket);

                try
                {
                    UdpSocket.SendTo(bytes.ToArray(), client.udpEndPoint);
                }
                catch (Exception e)
                {
                    if (Logger.includes.exceptions)
                        Logger.WriteError($"Failed to send UDP packet to {client.id}.", e);
                }
                client.udpPacket.Reset();
            }
        }

        protected abstract void Disconnect(ClientData client);
    }

}