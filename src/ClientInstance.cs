using System.Net.Sockets;
using System.Text;

namespace OwlTree
{
    public class ClientInstance : NetworkInstance
    {
        public ClientInstance(string addr, int port)
        {
            var client = new TcpClient();
            client.Connect(addr, port);

            var serverStream = client.GetStream();

            byte[] buffer = new byte[1024];

            serverStream.Write(Encoding.UTF8.GetBytes("Hello From Client"));
            serverStream.Read(buffer, 0, buffer.Length);
            var message = Encoding.UTF8.GetString(buffer, 0, buffer.Length);
            Console.WriteLine(message);
        }
    }
}