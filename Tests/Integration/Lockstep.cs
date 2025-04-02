using OwlTree;
using FileInitializer;

namespace Integration;

// test snapshot simulation buffer

public class LockstepTest
{

    // create server authoritative
    [Fact]
    public void Relayed()
    {
        Logs.InitPath("logs/Lockstep/Relayed");
        Logs.InitFiles(
            "logs/Lockstep/Relayed/server.log",
            "logs/Lockstep/Relayed/client1.log",
            "logs/Lockstep/Relayed/client2.log",
            "logs/Lockstep/Relayed/client3.log"
        );

        var server = new Connection(new Connection.Args{
            role = NetRole.Relay,
            simulationSystem = SimulationSystem.Lockstep,
            serverAddr = "127.0.0.1",
            udpPort = 0,
            tcpPort = 0,
            logger = (str) => File.AppendAllText("logs/Lockstep/Relayed/server.log", str),
            verbosity = Logger.Includes().All()
        });

        var client1 = new Connection(new Connection.Args{
            role = NetRole.Client,
            simulationSystem = SimulationSystem.Lockstep,
            serverAddr = "127.0.0.1",
            udpPort = server.ServerUdpPort,
            tcpPort = server.ServerTcpPort,
            logger = (str) => File.AppendAllText("logs/Lockstep/Relayed/client1.log", str),
            verbosity = Logger.Includes().SimulationEvents().ClientEvents().SpawnEvents().LogSeparators()
        });

        int iters = 0;
        while (!client1.IsReady && iters < 100)
        {
            iters++;
            server.ExecuteQueue();
            client1.ExecuteQueue();
            Thread.Sleep(server.TickRate);
        }
        Assert.True(client1.IsReady, "client1 failed to connect");

        var client2 = new Connection(new Connection.Args{
            role = NetRole.Client,
            simulationSystem = SimulationSystem.Lockstep,
            serverAddr = "127.0.0.1",
            udpPort = server.ServerUdpPort,
            tcpPort = server.ServerTcpPort,
            logger = (str) => File.AppendAllText("logs/Lockstep/Relayed/client2.log", str),
            verbosity = Logger.Includes().SimulationEvents().ClientEvents().SpawnEvents().LogSeparators()
        });

        iters = 0;
        while (!client2.IsReady && iters < 100)
        {
            iters++;
            server.ExecuteQueue();
            client1.ExecuteQueue();
            client2.ExecuteQueue();
            Thread.Sleep(server.TickRate);
        }
        Assert.True(client2.IsReady, "client2 failed to connect");

        // var client3 = new Connection(new Connection.Args{
        //     role = NetRole.Client,
        //     simulationSystem = SimulationBufferControl.Lockstep,
        //     serverAddr = "127.0.0.1",
        //     udpPort = server.ServerUdpPort,
        //     tcpPort = server.ServerTcpPort,
        //     logger = (str) => File.AppendAllText("logs/Lockstep/Relayed/client3.log", str),
        //     verbosity = Logger.Includes().SimulationEvents().ClientEvents().SpawnEvents().LogSeparators()
        // });

        // while (!client3.IsReady)
        // {
        //     server.ExecuteQueue();
        //     client1.ExecuteQueue();
        //     client2.ExecuteQueue();
        //     client3.ExecuteQueue();
        //     Thread.Sleep(server.TickRate);
        // }

        var client1Obj = client1.Spawn<LockstepTestObject>();

        for (int i = 0; i < 200; i++)
        {
            server.ExecuteQueue();
            client1.ExecuteQueue();
            client2.ExecuteQueue();
            // client3.ExecuteQueue();

            client1Obj.SendUpdate(client1Obj.client1Val + 1, client1.LocalTick);
            client1.Log($"sent {client1Obj.client1Val + 1} at {client1.LocalTick}");

            if (client2.TryGetObject(client1Obj.Id, out LockstepTestObject client2Obj))
            {
                client2Obj.SendUpdate(client2Obj.client2Val + 1, client2.LocalTick);
                client2.Log($"sent {client2Obj.client2Val + 1} at {client2.LocalTick}");
            }
            
            // if (client3.TryGetObject(client1Obj.Id, out LockstepTestObject client3Obj))
            // {
            //     client3Obj.SendUpdate(client3Obj.client3Val + 1, client3.CurTick);
            //     client3.Log($"sent {client3Obj.client3Val + 1} at {client3.CurTick}");
            // }

            Thread.Sleep(server.TickRate);
        }

        client1.Disconnect();
        client2.Disconnect();
        // client3.Disconnect();
        server.Disconnect();
    }

    public class LockstepTestObject : NetworkObject
    {
        public int client1Val = 0;
        public int client2Val = 0;
        public int client3Val = 0;
        
        [Rpc(RpcPerms.AnyToAll, InvokeOnCaller = true)]
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