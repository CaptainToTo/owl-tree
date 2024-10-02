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
                threaded = true,
            });
            server.OnClientConnected += (ClientId id) => {
                radio = server.Spawn<Radio>();
                radio.RPC_PingClients("Hello from server: 0");
            };
            Loop(server);
        }
        else if (args[0] == "c")
        {
            var client = new Connection(new Connection.Args
            {
                role = Connection.Role.Client,
                threaded = true,
            });
            client.OnObjectSpawn += (obj) => {
                radio = (Radio)obj;
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
            Thread.Sleep(1000);
        }
    }
}
