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
        /// <param name="addr">The server's IP address.</param>
        /// <param name="port">The port the server is listening to.</param>
        /// <param name="bufferSize">The size of read and write buffers in bytes. Exceeding the size of these buffers will result in lost data.</param>
        public ClientBuffer(string addr, int port, int bufferSize) : base(addr, port, bufferSize)
        {
            _client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint endPoint = new IPEndPoint(Address, port);
            _client.Connect(endPoint);

            _readList = new Socket[1]{_client};

            _outgoingBytes = new MessageBuffer(bufferSize);
        }

        // client state
        private Socket _client;
        private Socket[] _readList;
        private List<ClientId> _clients = new List<ClientId>();

        // messages to be sent ot the sever
        private MessageBuffer _outgoingBytes;

        /// <summary>
        /// Reads any data currently on the socket. Putting new messages in the queue.
        /// Blocks infinitely while waiting for the server to initially assign the buffer a ClientId.
        /// </summary>
        public override void Read()
        {
            if (!_client.Connected)
            {
                _client.Close();
                OnClientDisconnected?.Invoke(LocalId);
                return;
            }
            _readList[0] = _client;
            Socket.Select(_readList, null, null, IsReady ? 0 : -1);

            byte[] data = new byte[BufferSize];
            List<byte[]> messages = new List<byte[]>();

            foreach (var socket in _readList)
            {
                if (socket == _client)
                {
                    int dataLen = -1;
                    try
                    {
                        dataLen = socket.Receive(data);
                    }
                    catch { }

                    // disconnect if receive fails
                    if (dataLen <= 0)
                    {
                        socket.Close();
                        OnClientDisconnected?.Invoke(LocalId);
                        return;
                    }
                    // Console.WriteLine(BitConverter.ToString(data));

                    messages.Clear();
                    MessageBuffer.SplitMessageBytes(data, ref messages);

                    foreach (var message in messages)
                    {
                        RpcId clientMessage = ClientMessageDecode(message, out var clientId);
                        if (RpcId.CLIENT_CONNECTED_MESSAGE_ID <= clientMessage && clientMessage <= RpcId.CLIENT_DISCONNECTED_MESSAGE_ID)
                        {
                            HandleClientConnectionMessage(clientMessage, clientId);
                        }
                        else if (NetworkSpawner.TryDecode(message, out var rpcId, out var args))
                        {
                            _incoming.Enqueue(new Message(ClientId.None, LocalId, rpcId, NetworkId.None, args));
                        }
                        else
                        {
                            args = RpcAttribute.DecodeRpc(ClientId.None, message, out var protocol, out var target);
                            _incoming.Enqueue(new Message(ClientId.None, LocalId, protocol.Id, target, args));
                        }
                    }
                }
            }
        }

        // handle connections and disconnections immediately, 
        // they do not preserve the message execution order.
        private void HandleClientConnectionMessage(RpcId messageType, ClientId id)
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
                var span = _outgoingBytes.GetSpan(RpcAttribute.RpcExpectedLength(message.rpcId, message.args));
                RpcAttribute.EncodeRpc(span, message.rpcId, message.target, message.args);
            }
            _client.Send(_outgoingBytes.GetBuffer());
            _outgoingBytes.Reset();
        }

        /// <summary>
        /// Disconnect the client from the server.
        /// Invokes <c>OnClientDisconnected</c> with the local ClientId.
        /// </summary>
        public override void Disconnect()
        {
            _client.Close();
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