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

            _outgoing = new MessageBuffer(bufferSize);
        }

        // client state
        private Socket _client;
        private Socket[] _readList;
        private List<ClientId> _clients = new List<ClientId>();

        // messages to be sent ot the sever
        private MessageBuffer _outgoing;

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

                    messages.Clear();
                    MessageBuffer.SplitMessageBytes(data, ref messages);

                    foreach (var message in messages)
                    {
                        int clientMessage;
                        if ((clientMessage = ClientMessageDecode(message, out var clientId)) >= RpcProtocol.CLIENT_CONNECTED_MESSAGE_ID)
                        {
                            HandleClientConnectionMessage(clientMessage, clientId);
                        }
                        else
                        {
                            _incoming.Enqueue(new Message(ClientId.None, message));
                        }
                    }
                }
            }
        }

        // handle connections and disconnections immediately, 
        // they do not preserve the message execution order.
        private void HandleClientConnectionMessage(int messageType, ClientId id)
        {
            switch (messageType)
            {
                case RpcProtocol.CLIENT_CONNECTED_MESSAGE_ID:
                    _clients.Add(id);
                    OnClientConnected?.Invoke(id);
                    break;
                case RpcProtocol.LOCAL_CLIENT_CONNECTED_MESSAGE_ID:
                    _clients.Add(id);
                    LocalId = id;
                    IsReady = true;
                    OnReady?.Invoke(LocalId);
                    break;
                case RpcProtocol.CLIENT_DISCONNECTED_MESSAGE_ID:
                    _clients.Remove(id);
                    OnClientDisconnected?.Invoke(id);
                    break;
                default: break;
            }
        }
        
        /// <summary>
        /// Add message to the outgoing buffer.
        /// Actually write the buffer to the socket with <c>Write()</c>.
        /// </summary>
        public override void Write(byte[] message)
        {
            try
            {
                _outgoing.Add(message);
            }
            catch { }
        }

        /// <summary>
        /// INVALID ON CLIENTS. Clients cannot directly send messages to other clients.
        /// To do this, send a message to the server with <c>Send()</c> that contains the intended
        /// recipient. The server can then pass that message to the recipient.
        /// </summary>
        public override void WriteTo(ClientId id, byte[] message)
        {
            throw new InvalidOperationException("Clients cannot directly send messages to other clients.");
        }

        /// <summary>
        /// Write current outgoing buffer to the server socket.
        /// Buffer is cleared after writing.
        /// </summary>
        public override void Send()
        {
            _client.Send(_outgoing.GetBuffer());
            _outgoing.Reset();
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