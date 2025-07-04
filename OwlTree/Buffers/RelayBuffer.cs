
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
    internal class RelayBuffer : HostBuffer
    {
        public RelayBuffer(Args args, int maxClients, long requestTimeout, string hostAddr, IPAddress[] whitelist) : base(args, maxClients, requestTimeout, whitelist)
        {
            if (hostAddr != null)
                _hostAddr = IPAddress.Parse(hostAddr);
            
            IsReady = true;
            AddReadyMessage(LocalId);
        }

        private IPAddress _hostAddr = null;

        public override void Recv()
        {
            if (!IsActive)
                return;

            ResetReadList();

            foreach (var socket in ReadList)
            {
                // new client connects
                if (socket == TcpSocket)
                {
                    var clientData = TryAddClient();

                    if (clientData == null)
                        continue;

                    if (Authority == ClientId.None && (_hostAddr == null || _hostAddr.Equals(((IPEndPoint)clientData.tcpSocket.RemoteEndPoint).Address)))
                    {
                        _hostAddr = ((IPEndPoint)clientData.tcpSocket.RemoteEndPoint).Address;
                        Authority = clientData.id;

                        if (Logger.includes.connectionAttempts)
                            Logger.Write($"Client {clientData.id} assigned as host.");
                    }

                    SendNewClient(clientData);
                }
                else if (socket == UdpSocket.Socket) // receive client udp messages
                {
                    while (UdpSocket.Available > 0)
                    {
                        (var result, var source) = RecvUdpPacket();

                        if (result == RudpResult.Failed)
                            break;
                        
                        else if (result == RudpResult.PingRequest)
                            ParsePingRequest(source);
                        
                        // try to verify a new client connection
                        else if (result == RudpResult.UnregisteredEndpoint)
                        {
                            var responseCode = ProcessConnectionRequest(source);

                            // if the pre-assigned host hasn't connected yet, no-one else can join 
                            if (_hostAddr != null && Authority == ClientId.None && !_hostAddr.Equals(source.Address))
                            {
                                Logger.Write("Connection attempt from " + source.Address.ToString() + " (udp port: " + source.Port + ") rejected.");
                                continue;
                            }

                            if (responseCode == ConnectionResponseCode.Accepted)
                                ConnectRequests.Add(source);

                            SendConnectionResponse(source, responseCode);
                        }
                    }
                }
                else // receive client tcp messages
                {
                    Array.Clear(ReadBuffer, 0, ReadBuffer.Length);
                    int dataRemaining = -1;
                    int dataLen = -1;
                    ClientData client = FindClientData(socket);

                    if (client == null)
                        continue;

                    do
                    {
                        (dataRemaining, dataLen) = RecvTcpPacket(socket, client, dataRemaining, dataLen);

                        if (dataLen <= 0)
                            break;

                        ReadPacket.StartMessageRead();
                        while (ReadPacket.TryGetNextMessage(out var bytes))
                        {
                            try
                            {
                                var rpcId = new RpcId(bytes);

                                if (rpcId.Id == RpcId.ClientDisconnectedId && client.id == Authority)
                                    Disconnect(new ClientId(bytes.Slice(rpcId.ByteLength())));

                                else if (rpcId.Id == RpcId.HostMigrationId && client.id == Authority)
                                    MigrateHost(new ClientId(bytes.Slice(rpcId.ByteLength())));

                                else if (rpcId.IsObjectEvent() && client.id == Authority)
                                    RelayTcpMessage(bytes, client.id);
                                
                                else if (rpcId.Id == RpcId.PingRequestId && Encoder.TryPingRequestDecode(bytes, out var request))
                                    HandlePingRequest(request, Protocol.Tcp);
                                
                                else if (rpcId.IsTickEvent())
                                {
                                    Encoder.DecodeClients(bytes.Slice(RpcId.MaxByteLength), out var caller, out var callee);
                                    if (rpcId == RpcId.CurTickId && caller == client.id && client.id == Authority)
                                        RelayMessageTo(bytes, ClientData.Find(callee).tcpPacket);
                                    else if (rpcId == RpcId.NextTickId)
                                        RelayTcpMessage(bytes, client.id);
                                }
                                else if (rpcId >= RpcId.FirstRpcId)
                                {
                                    Encoder.DecodeRpcHeader(bytes, out rpcId, out var caller, out var callee, out var target);
                                    if (caller != client.id) continue;

                                    if (callee == ClientId.None)
                                        RelayTcpMessage(bytes, client.id);
                                    else
                                        RelayMessageTo(bytes, ClientData.Find(callee).tcpPacket);
                                }
                            }
                            catch (Exception e)
                            {
                                if (Logger.includes.exceptions)
                                    Logger.WriteError($"Failed to relay TCP message '{Encoder.ToString(bytes)}' from {client.id}.", e);
                            }
                        }
                    } while (dataRemaining > 0);
                }
            }

            UdpSocket.RequestMissingPackets();

            while (UdpSocket.TryGetNextPacket(out var packet, out var source))
            {
                ReadPacket.Clear();
                ReadPacket.FromBytes(packet, 0, packet.Length);

                var client = ClientData.Find(source);

                if (client.hash != ReadPacket.header.hash)
                {
                    if (Logger.includes.exceptions)
                        Logger.Write($"Incorrect hash received in UDP packet from client {client.id}. Got {ReadPacket.header.hash}, but expected {client.hash}. Ignoring packet.");
                    continue;
                }

                if (Logger.includes.udpPostTransform)
                    Logger.WriteRecv($"mutated Post-Transform UDP packet from {client.id}:", ReadPacket);

                ApplyRecvSteps(ReadPacket);

                if (Logger.includes.udpPreTransform)
                    Logger.WriteRecv($"original Pre-Transform UDP packet from {client.id}:", ReadPacket);

                ReadPacket.StartMessageRead();
                while (ReadPacket.TryGetNextMessage(out var bytes))
                {
                    try
                    {
                        var rpcId = new RpcId(bytes);
                        if (rpcId.Id == RpcId.PingRequestId && Encoder.TryPingRequestDecode(bytes, out var request))
                            HandlePingRequest(request, Protocol.Udp);
                        
                        else if (rpcId >= RpcId.FirstRpcId)
                        {
                            Encoder.DecodeRpcHeader(bytes, out rpcId, out var caller, out var callee, out var target);
                            if (caller != client.id) continue;

                            if (callee == ClientId.None)
                                RelayUdpMessage(bytes, client.id);
                            else
                                RelayMessageTo(bytes, ClientData.Find(callee).udpPacket);
                        }
                        else if (rpcId == RpcId.NextTickId)
                            RelayUdpMessage(bytes, client.id);
                    }
                    catch (Exception e)
                    {
                        if (Logger.includes.exceptions)
                            Logger.WriteError($"Failed to relay UDP message '{Encoder.ToString(bytes)}' from {client.id}.", e);
                    }
                }
            }
        }

        private void RelayTcpMessage(ReadOnlySpan<byte> bytes, ClientId source)
        {
            foreach (var client in ClientData)
            {
                if (client.id == source) continue;
                RelayMessageTo(bytes, client.tcpPacket);
            }
        }

        private void RelayUdpMessage(ReadOnlySpan<byte> bytes, ClientId source)
        {
            foreach (var client in ClientData)
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

        public override void Send()
        {
            while (MessageQueue.TryGetNextOutgoing(out var message))
            {
                if (HandleClientEvent(message))
                {
                    if (!IsActive)
                        return;
                    continue;
                }
                else if (message.rpcId == RpcId.PingRequestId && Encoder.TryPingRequestDecode(message.bytes, out var request))
                    SendPingRequest(message, request);
                else
                    AddMessageToPackets(message);
            }
            foreach (var client in ClientData)
            {
                SendTcpPackets(client);
                SendUdpPackets(client);
            }

            HasClientEvent = false;
            HasRelayMessages = false;
        }

        public override void Disconnect()
        {
            if (!IsActive)
                return;
            
            IsReady = false;
            IsActive = false;

            var ids = ClientData.GetIds();
            foreach (var id in ids)
            {
                if (id == Authority) continue;
                Disconnect(id);
            }
            Disconnect(Authority);
            if (!TcpSocket.Connected)
                TcpSocket.Close();
            UdpSocket.Close();
            AddClientDisconnectedMessage(LocalId);
        }

        public override void Disconnect(ClientId id)
        {
            var client = ClientData.Find(id);
            if (client != null)
                Disconnect(client);
        }

        protected override void Disconnect(ClientData client)
        {
            if (client == null)
                return;
            
            // migrate before disconnect
            if (client.id == Authority && Migratable)
            {
                if (!ShutdownWhenEmpty && ClientData.Count <= 1)
                {
                    Authority = ClientId.None;
                    _hostAddr = null;
                    AddHostMigrationMessage(Authority);
                }
                else if (ClientData.Count > 1)
                {
                    MigrateHost(FindNewHost());
                }
            }
            
            ClientData.Remove(client);
            UdpSocket.RemoveEndpoint(client.udpEndPoint);
            client.tcpSocket.Close();
            AddClientDisconnectedMessage(client.id);

            foreach (var otherClient in ClientData)
            {
                var span = otherClient.tcpPacket.GetSpan(Encoder.ClientMessageLength);
                Encoder.ClientDisconnectEncode(span, client.id);
            }
            HasClientEvent = true;

            // shutdown after disconnect
            if (client.id == Authority)
            {
                if (!Migratable)
                    Disconnect();
                else if (ShutdownWhenEmpty && ClientData.Count <= 0)
                    Disconnect();
            }
        }

        // decide new host by lowest latency
        private ClientId FindNewHost()
        {
            ClientData best = null;
            foreach (var client in ClientData)
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
            var data = ClientData.Find(newHost);
            if (data == null)
                return;
            Authority = newHost;
            _hostAddr = data.Address;
            foreach (var client in ClientData)
            {
                var span = client.tcpPacket.GetSpan(Encoder.ClientMessageLength);
                Encoder.HostMigrationEncode(span, newHost);
            }
            HasClientEvent = true;

            AddHostMigrationMessage(newHost);
        }
    }
}