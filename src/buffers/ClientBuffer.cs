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

            _readList = [_client];

            _outgoing = new MessageBuffer(bufferSize);
        }

        // client state
        private Socket _client;
        private Socket[] _readList;
        private List<ClientId> _clients = new List<ClientId>();

        public ClientId LocalId { get; private set; }
        
        // currently read messages
        private Queue<Message> _incoming = new Queue<Message>();

        // messages to be sent ot the sever
        private MessageBuffer _outgoing;

        /// <summary>
        /// Get the next message in the read queue.
        /// </summary>
        /// <param name="message">The next message.</param>
        /// <returns>True if there is a message, false if the queue is empty.</returns>
        public bool GetNextMessage(out Message message)
        {
            if (_incoming.Count == 0)
            {
                message = Message.Empty;
                return false;
            }
            message = _incoming.Dequeue();
            return true;
        }

        /// <summary>
        /// Reads any data currently on the socket. Putting new messages in the queue.
        /// </summary>
        public void Read()
        {
            Socket.Select(_readList, null, null, 0);

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
                        _incoming.Enqueue(new Message(ClientId.None, LocalId, message));
                    }
                }
            }
        }
        
        /// <summary>
        /// Add message to the outgoing buffer.
        /// Actually write the buffer to the socket with <c>Write()</c>.
        /// </summary>
        public void Send(byte[] message)
        {
            try
            {
                _outgoing.Add(message);
            }
            catch { }
        }

        /// <summary>
        /// Write current outgoing buffer to the server socket.
        /// Buffer is cleared after writing.
        /// </summary>
        public void Write()
        {
            _client.Send(_outgoing.GetBuffer());
            _outgoing.Reset();
        }

        /// <summary>
        /// Disconnect the client from the server.
        /// Invokes <c>OnClientDisconnected</c> with the local ClientId.
        /// </summary>
        public void Disconnect()
        {
            _client.Close();
            OnClientDisconnected?.Invoke(LocalId);
        }
    }
}