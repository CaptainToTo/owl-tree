// See https://aka.ms/new-console-template for more information
using System;
using System.Text;
using OwlTree;

class Program
{
    static void Main(string[] args)
    {
        // RpcAttribute.GenerateRpcProtocols();
        // var obj = new NetworkObject();
        // obj.TestRpc(obj.Id, 5);
        // return;
        if (args[0] == "s")
        {
            var server = new Connection(new Connection.ConnectionArgs
            {
                role = Connection.Role.Server
            });
            server.OnClientConnected += (ClientId id) => Console.WriteLine(id.ToString());
            server.OnClientConnected += (ClientId id) => {
                var netObj = server.Spawn<NetworkObject>();
                // server.Send();
                server.Destroy(netObj);
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
            client.OnReady += (ClientId id) => Console.WriteLine("Local Id: " + id.ToString());
            client.Read();
            Loop(client);
        }
    }

    public static void Loop(Connection connection)
    {
        while (true)
        {
            connection.Read();
            if (!connection.IsActive)
                break;
        }
    }
}
