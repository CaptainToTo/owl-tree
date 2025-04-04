using OwlTree;
using FileInitializer;

namespace Integration;

// test snapshot simulation buffer

public class SnapshotTest
{

    // create server authoritative
    [Fact]
    public void ServerAuthoritative()
    {
        Logs.InitPath("logs/Snapshot/ServerAuth");
        Logs.InitFiles(
            "logs/Snapshot/ServerAuth/server.log",
            "logs/Snapshot/ServerAuth/client.log"
        );

        var server = new Connection(new Connection.Args{
            role = NetRole.Server,
            simulationSystem = SimulationSystem.Snapshot,
            serverAddr = "127.0.0.1",
            udpPort = 0,
            tcpPort = 0,
            logger = (str) => File.AppendAllText("logs/Snapshot/ServerAuth/server.log", str),
            verbosity = Logger.Includes().SimulationEvents().ClientEvents().SpawnEvents().LogSeparators()
        });

        var client = new Connection(new Connection.Args{
            role = NetRole.Client,
            simulationSystem = SimulationSystem.Snapshot,
            serverAddr = "127.0.0.1",
            udpPort = server.ServerUdpPort,
            tcpPort = server.ServerTcpPort,
            logger = (str) => File.AppendAllText("logs/Snapshot/ServerAuth/client.log", str),
            verbosity = Logger.Includes().SimulationEvents().ClientEvents().SpawnEvents().LogSeparators()
        });
        
        int iters = 0;
        while (!client.IsReady && iters < 100)
        {
            iters++;
            server.ExecuteQueue();
            client.ExecuteQueue();
            Thread.Sleep(server.TickRate);
        }
        var serverObj = server.Spawn<TestObject>();

        Assert.True(client.IsReady, "client failed to connect");

        for (int i = 0; i < 200; i++)
        {
            server.ExecuteQueue();
            client.ExecuteQueue();
            serverObj.SendServerUpdate(serverObj.serverVal + 1, server.LocalTick);
            if (client.TryGetObject<TestObject>(serverObj.Id, out var clientObj))
            {
                client.Log($"server val: {clientObj.serverVal}, tick: {clientObj.lastServerTick}");
                clientObj.SendClientUpdate(clientObj.clientVal + 1, client.LocalTick);
            }
            server.Log($"client val: {serverObj.clientVal}, tick: {serverObj.lastClientTick}");
            Thread.Sleep(server.TickRate);
        }

        client.Disconnect();
        server.Disconnect();
    }
    
    public class TestObject : NetworkObject
    {
        public int serverVal = 0;
        public Tick lastServerTick;
        
        public int clientVal = 0;
        public Tick lastClientTick;
        
        [Rpc(RpcPerms.AuthorityToClients, InvokeOnCaller = true)]
        public virtual void SendServerUpdate(int newVal, Tick tick)
        {
            serverVal = newVal;
            lastServerTick = tick;
        }

        [Rpc(RpcPerms.ClientsToAuthority, InvokeOnCaller = true)]
        public virtual void SendClientUpdate(int newVal, Tick tick)
        {
            clientVal = newVal;
            lastClientTick = tick;
        }
    }
}
