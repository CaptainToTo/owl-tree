using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

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
        /// <param name="owlTreeVer">The version of Owl Tree packets will be formatted according to.</param>
        /// <param name="appVer">The version of your app this connection is running on.</param>
        /// <param name="addr">The server's IP address.</param>
        /// <param name="addr">The server's IP address.</param>
        /// <param name="tpcPort">The port to bind the TCP socket to.</param>
        /// <param name="serverUdpPort">The port to bind the UDP socket to.</param>
        /// <param name="bufferSize">The size of read and write buffers in bytes.</param>
        public ClientBuffer(ushort owlTreeVer, ushort appVer, string addr, int tcpPort, int serverUdpPort, int clientUdpPort, int bufferSize, Decoder decoder, Encoder encoder) : base(owlTreeVer, appVer, addr, tcpPort, serverUdpPort, clientUdpPort, bufferSize, decoder, encoder)
        {
            _tcpClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint endPoint = new IPEndPoint(Address, TcpPort);
            _tcpClient.Connect(endPoint);

            _udpClient = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _udpClient.Bind(new IPEndPoint(Address, ClientUdpPort));

            _udpEndPoint = new IPEndPoint(Address, ServerUdpPort);

            _readList.Add(_tcpClient);
            _readList.Add(_udpClient);

            _tcpPacket = new Packet(bufferSize);
            _tcpPacket.header.owlTreeVer = OwlTreeVersion;
            _tcpPacket.header.appVer = AppVersion;
            _udpPacket = new Packet(bufferSize, true);
            _udpPacket.header.owlTreeVer = OwlTreeVersion;
            _udpPacket.header.appVer = AppVersion;
        }

        // client state
        private Socket _tcpClient;
        private Socket _udpClient;
        private IPEndPoint _udpEndPoint;
        private List<Socket> _readList = new List<Socket>();
        private List<ClientId> _clients = new List<ClientId>();

        // messages to be sent ot the sever
        private Packet _tcpPacket;
        private Packet _udpPacket;

        /// <summary>
        /// Reads any data currently on the socket. Putting new messages in the queue.
        /// Blocks infinitely while waiting for the server to initially assign the buffer a ClientId.
        /// </summary>
        public override void Read()
        {
            if (!_tcpClient.Connected)
            {
                return;
            }
            _readList.Clear();
            _readList.Add(_tcpClient);
            _readList.Add(_udpClient);
            Socket.Select(_readList, null, null, IsReady ? 0 : -1);

            foreach (var socket in _readList)
            {
                if (socket == _tcpClient)
                {
                    Array.Clear(ReadBuffer, 0, ReadBuffer.Length);
                    ReadPacket.Clear();

                    int dataLen = -1;
                    try
                    {
                        int iters = 0;
                        do {
                            dataLen = socket.Receive(ReadBuffer);
                            ReadPacket.FromBytes(ReadBuffer);
                            iters++;
                        } while (ReadPacket.Incomplete && iters < 5);
                    }
                    catch { }

                    // disconnect if receive fails
                    if (dataLen <= 0)
                    {
                        socket.Close();
                        OnClientDisconnected?.Invoke(LocalId);
                        return;
                    }

                    ApplyReadSteps(ReadPacket);
                    
                    ReadPacket.StartMessageRead();
                    while (ReadPacket.TryGetNextMessage(out var bytes))
                    {
                        RpcId clientMessage = ClientMessageDecode(bytes, out var clientId, out var hash);
                        if (RpcId.CLIENT_CONNECTED_MESSAGE_ID <= clientMessage && clientMessage <= RpcId.CLIENT_DISCONNECTED_MESSAGE_ID)
                        {
                            HandleClientConnectionMessage(clientMessage, clientId, hash);
                        }
                        else if (TryDecode(ClientId.None, bytes, out var message))
                        {
                            _incoming.Enqueue(message);
                        }
                    }
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
                        ReadPacket.FromBytes(ReadBuffer);
                    }
                    catch { }

                    if (dataLen <= 0)
                    {
                        continue;
                    }

                    ApplyReadSteps(ReadPacket);

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
        private void HandleClientConnectionMessage(RpcId messageType, ClientId id, UInt32 hash)
        {
            switch (messageType.Id)
            {
                case RpcId.CLIENT_CONNECTED_MESSAGE_ID:
                    _clients.Add(id);
                    OnClientConnected?.Invoke(id);
                    break;
                case RpcId.LOCAL_CLIENT_CONNECTED_MESSAGE_ID:
                    _clients.Add(id);
                    LocalId = id;
                    _tcpPacket.header.sender = LocalId.Id;
                    _tcpPacket.header.hash = hash;
                    _udpPacket.header.sender = LocalId.Id;
                    _udpPacket.header.hash = hash;
                    IsReady = true;
                    OnReady?.Invoke(LocalId);
                    break;
                case RpcId.CLIENT_DISCONNECTED_MESSAGE_ID:
                    _clients.Remove(id);
                    OnClientDisconnected?.Invoke(id);
                    break;
                default: break;
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
                ApplySendSteps(_tcpPacket);
                var bytes = _tcpPacket.GetPacket();
                _tcpClient.Send(bytes);
                _tcpPacket.Reset();
            }

            if (!_udpPacket.IsEmpty)
            {
                _udpPacket.header.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                ApplySendSteps(_udpPacket);
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
            _tcpClient.Close();
            _udpClient.Close();
            OnClientDisconnected?.Invoke(LocalId);
        }

        /// <summary>
        /// INVALID ON CLIENTS. Clients cannot disconnect other clients.
        /// </summary>
        public override void Disconnect(ClientId id)
        {
            throw new InvalidOperationException("Clients cannot disconnect other clients.");
        }
    }
}