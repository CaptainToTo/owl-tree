// See https://aka.ms/new-console-template for more information
using System;
using System.Text;
using OwlTree;

class Program
{
    static Radio? radio = null;

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
            server.OnClientConnected += (ClientId id) => Console.WriteLine("Client Joined: " + id.ToString());
            server.OnClientConnected += (ClientId id) => {
                radio = server.Spawn<Radio>();
                radio.RPC_PingClients("Hello from server");
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
            client.OnObjectSpawn += (obj) => {
                Console.WriteLine("Spawned: " + obj.ToString());
                radio = (Radio)obj;
            };
            client.OnObjectDestroy += (obj) => Console.WriteLine("Destroyed: " + obj.ToString());
            client.AwaitConnection();
            Loop(client);
        }
    }

    public static void Loop(Connection connection)
    {
        bool sent = false;
        while (true)
        {
            connection.Read();
            if (radio != null && !sent)
            {
                if (connection.role == Connection.Role.Server)
                {
                    
                }
                else
                {
                    // radio.RPC_PingServer("Hello from client");
                }
                connection.Send();
                sent = true;
            }
            if (!connection.IsActive)
                break;
        }
    }
}
