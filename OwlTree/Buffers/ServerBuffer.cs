
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
    internal class ServerBuffer : HostBuffer
    {
        /// <summary>
        /// Manages sending and receiving messages for a server instance.
        /// </summary>
        /// <param name="args">NetworkBuffer parameters.</param>
        /// <param name="maxClients">The max number of clients that can be connected at once.</param>
        public ServerBuffer(Args args, int maxClients, long requestTimeout, IPAddress[] whitelist) : base (args, maxClients, requestTimeout, whitelist)
        {
            Migratable = false;

            IsReady = true;
            AddReadyMessage(LocalId);
        }

        /// <summary>
        /// Reads any data currently on sockets. Putting new messages in the queue, and connecting new clients.
        /// </summary>
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
                    if (clientData != null)
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
                                if (Encoder.TryPingRequestDecode(bytes, out var request))
                                    HandlePingRequest(request, Protocol.Tcp);
                                else
                                    Decode(client.id, bytes, Protocol.Tcp);
                            }
                            catch (Exception e)
                            {
                                if (Logger.includes.exceptions)
                                    Logger.WriteError($"Failed to decode TCP message '{Encoder.ToString(bytes)}' from {client.id}.", e);
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
                    Logger.WriteRecv($"original Post-Transform UDP packet from {client.id}:", ReadPacket);

                ReadPacket.StartMessageRead();
                while (ReadPacket.TryGetNextMessage(out var bytes))
                {
                    try
                    {
                        if (Encoder.TryPingRequestDecode(bytes, out var request))
                            HandlePingRequest(request, Protocol.Udp);
                        else
                            Decode(client.id, bytes, Protocol.Udp);
                    }
                    catch (Exception e)
                    {
                        if (Logger.includes.exceptions)
                            Logger.WriteError($"Failed to decode UDP message '{Encoder.ToString(bytes)}' from {client.id}.", e);
                    }
                }
            }
        }

        /// <summary>
        /// Write current buffers to client sockets.
        /// Buffers are cleared after writing.
        /// </summary>
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

        /// <summary>
        /// Disconnects all clients, and closes the server.
        /// </summary>
        public override void Disconnect()
        {
            if (!TcpSocket.Connected)
                return;
            var ids = ClientData.GetIds();
            foreach (var id in ids)
                Disconnect(id);
            TcpSocket.Close();
            UdpSocket.Close();
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
            var client = ClientData.Find(id);
            if (client != null)
                Disconnect(client);
        }

        protected override void Disconnect(ClientData client)
        {
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
        }

        public override void MigrateHost(ClientId newHost)
        {
            throw new InvalidOperationException("Servers cannot migrate authority off of themselves.");
        }
    }
}