using OwlTree;
using FileInitializer;

namespace Integration;

// test snapshot simulation buffer

public class RollbackTest
{

    // create server authoritative
    [Fact]
    public void Relayed()
    {
        Logs.InitPath("logs/Rollback/Relayed");
        Logs.InitFiles(
            "logs/Rollback/Relayed/server.log",
            "logs/Rollback/Relayed/client1.log",
            "logs/Rollback/Relayed/client2.log",
            "logs/Rollback/Relayed/client3.log"
        );

        var server = new Connection(new Connection.Args{
            role = NetRole.Relay,
            simulationSystem = SimulationSystem.Rollback,
            serverAddr = "127.0.0.1",
            udpPort = 0,
            tcpPort = 0,
            logger = (str) => File.AppendAllText("logs/Rollback/Relayed/server.log", str),
            verbosity = Logger.Includes().All()
        });

        var client1 = new Connection(new Connection.Args{
            role = NetRole.Client,
            simulationSystem = SimulationSystem.Rollback,
            serverAddr = "127.0.0.1",
            udpPort = server.ServerUdpPort,
            tcpPort = server.ServerTcpPort,
            logger = (str) => File.AppendAllText("logs/Rollback/Relayed/client1.log", str),
            verbosity = Logger.Includes().SimulationEvents().ClientEvents().SpawnEvents().LogSeparators()
        });

        while (!client1.IsReady)
        {
            server.ExecuteQueue();
            client1.ExecuteQueue();
            Thread.Sleep(server.TickRate);
        }

        var client2 = new Connection(new Connection.Args{
            role = NetRole.Client,
            simulationSystem = SimulationSystem.Rollback,
            serverAddr = "127.0.0.1",
            udpPort = server.ServerUdpPort,
            tcpPort = server.ServerTcpPort,
            logger = (str) => File.AppendAllText("logs/Rollback/Relayed/client2.log", str),
            verbosity = Logger.Includes().SimulationEvents().ClientEvents().SpawnEvents().LogSeparators()
        });

        while (!client2.IsReady)
        {
            server.ExecuteQueue();
            client1.ExecuteQueue();
            client2.ExecuteQueue();
            Thread.Sleep(server.TickRate);
        }

        var client3 = new Connection(new Connection.Args{
            role = NetRole.Client,
            simulationSystem = SimulationSystem.Rollback,
            serverAddr = "127.0.0.1",
            udpPort = server.ServerUdpPort,
            tcpPort = server.ServerTcpPort,
            logger = (str) => File.AppendAllText("logs/Rollback/Relayed/client3.log", str),
            verbosity = Logger.Includes().SimulationEvents().ClientEvents().SpawnEvents().LogSeparators()
        });

        while (!client3.IsReady)
        {
            server.ExecuteQueue();
            client1.ExecuteQueue();
            client2.ExecuteQueue();
            client3.ExecuteQueue();
            Thread.Sleep(server.TickRate);
        }

        var client1Obj = client1.Spawn<RollbackTestObject>();

        for (int i = 0; i < 200; i++)
        {
            server.ExecuteQueue();
            client1.ExecuteQueue();
            client2.ExecuteQueue();
            client3.ExecuteQueue();

            client1Obj.SendUpdate(client1Obj.client1Val + 1, client1.LocalTick);
            client1.Log($"sent {client1Obj.client1Val + 1} at {client1.LocalTick}");

            if (client2.TryGetObject(client1Obj.Id, out RollbackTestObject client2Obj))
            {
                client2Obj.SendUpdate(client2Obj.client2Val + 1, client2.LocalTick);
                client2.Log($"sent {client2Obj.client2Val + 1} at {client2.LocalTick}");
            }
            
            if (client3.TryGetObject(client1Obj.Id, out RollbackTestObject client3Obj))
            {
                client3Obj.SendUpdate(client3Obj.client3Val + 1, client3.LocalTick);
                client3.Log($"sent {client3Obj.client3Val + 1} at {client3.LocalTick}");
            }

            Thread.Sleep(server.TickRate);
        }

        client1.Disconnect();
        client2.Disconnect();
        client3.Disconnect();
        server.Disconnect();
    }

    public class RollbackTestObject : NetworkObject
    {
        public int client1Val = 0;
        public int client2Val = 0;
        public int client3Val = 0;
        
        [Rpc(RpcPerms.AnyToAll, InvokeOnCaller = true, RpcProtocol = Protocol.Udp)]
        public virtual void SendUpdate(int val, Tick tick, [CallerId] ClientId caller = default)
        {
            if (caller == new ClientId(1))
                client1Val = val;
            else if (caller == new ClientId(2))
                client2Val = val;
            else
                client3Val = val;
            Connection.Log($"from {caller}:\n    client1: {client1Val}, client2: {client2Val}, client3: {client3Val}, tick: {tick}");
        }
    }

    private static Connection GetHost(params Connection[] clients)
    {
        return clients.Where(c => c.IsHost).First();
    }
}