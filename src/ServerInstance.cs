
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace OwlTree
{
    public class ServerInstance : NetworkInstance
    {
        public ServerInstance(string addr, int port, byte maxClients)
        {
            address = IPAddress.Parse(addr);
            this.port = port;

            _server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint endPoint = new IPEndPoint(address, port);
            _server.Bind(endPoint);
            _server.Listen(maxClients);
            readList.Add(_server);
        }

        public int port { get; private set; } 
        public IPAddress address { get; private set; }

        private struct ClientBuffer
        {
            public PlayerId id;
            public SendBuffer buffer;
        }

        private Socket _server;
        List<Socket> readList = new List<Socket>();
        Dictionary<Socket, ClientBuffer> clients = new Dictionary<Socket, ClientBuffer>();

        private void HandleBuffers()
        {
            while (true)
            {
                Socket.Select(readList, null, null, 0);

                foreach (var socket in readList)
                {
                    if (socket == _server)
                    {
                        var client = socket.Accept();

                        readList.Add(client);
                        clients.Add(
                            client, 
                            new ClientBuffer {
                                id = new PlayerId(),
                                buffer = new SendBuffer(1024)
                            }
                        );
                    }
                }
            }
        }
    }
}