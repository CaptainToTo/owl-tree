﻿using OwlTree;

class Program
{
    static Radio? radio = null;

    static NetworkList<Capacity8, int> list = new NetworkList<Capacity8, int>();

    static void Main(string[] args)
    {
        list.Add(2);
        list.Add(10);
        list.Add(8);
        list.Add(500);
        if (args[0] == "s")
        {
            var server = new Connection(new Connection.Args
            {
                role = Connection.Role.Server,
                verbosity = Logger.Includes().AllTypeIds().AllRpcProtocols()
            });
            server.OnClientConnected += (ClientId id) => {
                if (radio == null)
                    radio = server.Spawn<Radio>();
            };
            // radio = server.Spawn<Radio>();
            Loop(server);
        }
        else if (args[0] == "c")
        {
            var client = new Connection(new Connection.Args
            {
                role = Connection.Role.Client,
                verbosity = Logger.Includes().AllTypeIds().AllRpcProtocols()
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
                radio.RPC_ListTest(list);
            }
            else if (connection.NetRole == Connection.Role.Client && radio != null)
            {
                radio.RPC_SendPosition(0.25f, 0.25f, 0.25f);
            }
            Thread.Sleep(1000);
        }
    }
}