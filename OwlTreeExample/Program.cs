using OwlTree;

class Program
{
    static Radio? radio = null;

    static void Main(string[] args)
    {
        if (args[0] == "s")
        {
            var server = new Connection(new Connection.Args
            {
                role = Connection.Role.Server,
            });
            server.OnClientConnected += (ClientId id) => {
                if (radio == null)
                    radio = server.Spawn<Radio>();
            };
            Loop(server);
        }
        else if (args[0] == "c")
        {
            var client = new Connection(new Connection.Args
            {
                role = Connection.Role.Client,
            });
            client.OnObjectSpawn += (obj) => {
                radio = (Radio)obj;
                // radio.RPC_PingServer("Hello from client: 0");
            };
            Loop(client);
        }
    }

    public static void Loop(Connection connection)
    {
        int tick = 0;
        while (true)
        {
            Console.WriteLine("Tick: " + tick);
            tick++;
            connection.ExecuteQueue();
            if (connection.NetRole == Connection.Role.Server && radio != null)
            {
                radio.RPC_ServerPosition(0.5f, 0.5f, 0.5f);
            }
            else if (connection.NetRole == Connection.Role.Client && radio != null)
            {
                radio.RPC_ClientPosition(0.25f, 0.25f, 0.25f);
            }
            Thread.Sleep(10);
        }
    }
}