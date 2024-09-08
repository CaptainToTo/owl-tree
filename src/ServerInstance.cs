
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

        public void Read()
        {
            Socket.Select(readList, null, null, 0);
            byte[] data = new byte[1024];

            List<byte[]> rpcBytes = new List<byte[]>();

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
                else
                {
                    int dataLen = -1;
                    try
                    {
                        dataLen = socket.Receive(data);
                    }
                    catch 
                    { 
                        Console.WriteLine("Client disconnected unexpectedly.");
                    }

                    var clientBuffer = clients[socket];

                    if (dataLen <= 0)
                    {
                        clients.Remove(socket);
                        readList.Remove(socket);
                        socket.Close();
                        continue;
                    }

                    rpcBytes.Clear();
                    SendBuffer.GetRpcBytes(data, ref rpcBytes);
                    
                    foreach (var rpcEncoding in rpcBytes)
                    {
                        try
                        {
                            // decode rpc
                            // apply rpc
                        }
                        catch
                        {
                            Console.WriteLine("Failed to apply RPC");
                        }
                    }
                }
            }
        }
    }
}