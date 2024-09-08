
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

            public ConnectionArgs() { }
        }

        public Connection(ConnectionArgs args)
        {
            if (args.role == Role.Client)
            {
                _tcpStream = new ClientInstance(args.serverAddr, args.port);
            }
            else
            {
                _tcpStream = new ServerInstance(args.serverAddr, args.port, args.maxClients);
            }
            role = args.role;
        }

        public Role role { get; private set; }

        private NetworkInstance _tcpStream;
    }
}