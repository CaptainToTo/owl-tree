// See https://aka.ms/new-console-template for more information
using System;
using System.Text;
using OwlTree;

class Program
{
    static void Main(string[] args)
    {
        if (args[0] == "s")
        {
            var server = new Connection(new Connection.ConnectionArgs
            {
                role = Connection.Role.Server
            });
            server.OnClientConnected += (ClientId id) => Console.WriteLine(id.ToString());
            server.OnClientConnected += (ClientId id) => {
                server.WriteTo(id, Encoding.UTF8.GetBytes("Hello from server"));
                server.Send();
            };
            Loop(server);
        }
        else if (args[0] == "c")
        {
            var client = new Connection(new Connection.ConnectionArgs
            {
                role = Connection.Role.Client
            });
            client.OnReady += (ClientId id) => Console.WriteLine(id.ToString());
            client.Read();
            Loop(client);
        }
    }

    public static void Loop(Connection connection)
    {
        while (true)
        {
            connection.Read();
            while (connection.GetNextMessage(out var message))
            {
                if (message.bytes != null)
                    Console.WriteLine(Encoding.UTF8.GetString(message.bytes));
            }
        }
    }
}
