
namespace OwlTree
{
    public class Connection
    {
        public enum Role
        {
            Client,
            Server
        }

        public struct ConnectionArgs
        {
            public Role role = Role.Server;
            public string serverAddr = "127.0.0.1";
            public int port = 8080;
            public byte maxClients = 4;
            public int bufferSize = 2048;

            public ConnectionArgs() { }
        }

        public Connection(ConnectionArgs args)
        {
            if (args.role == Role.Client)
            {
                _buffer = new ClientBuffer(args.serverAddr, args.port, args.bufferSize);
            }
            else
            {
                _buffer = new ServerBuffer(args.serverAddr, args.port, args.maxClients, args.bufferSize);
            }
            role = args.role;
            _buffer.OnClientConnected = (id) => OnClientConnected?.Invoke(id);
            _buffer.OnClientDisconnected = (id) => {
                if (id == _buffer.LocalId)
                    IsActive = false;
                OnClientDisconnected?.Invoke(id);
            };
            _buffer.OnReady = (id) => OnReady?.Invoke(id);
        }

        public Role role { get; private set; }

        private NetworkBuffer _buffer;

        public event ClientId.IdEvent? OnClientConnected;

        public event ClientId.IdEvent? OnClientDisconnected;

        public event ClientId.IdEvent? OnReady;

        public bool IsActive { get; private set; } = true;

        public void Read()
        {
            _buffer.Read();
        }

        public bool GetNextMessage(out NetworkBuffer.Message message)
        {
            return _buffer.GetNextMessage(out message);
        }

        public void Write(byte[] message)
        {
            _buffer.Write(message);
        }

        public void WriteTo(ClientId id, byte[] message)
        {
            _buffer.WriteTo(id, message);
        }

        public void Send()
        {
            _buffer.Send();
        }

        public void Disconnect()
        {
            _buffer.Disconnect();
            IsActive = false;
        }

        public void Disconnect(ClientId id)
        {
            _buffer.Disconnect(id);
        }
    }
}