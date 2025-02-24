using OwlTree;
using FileInitializer;

namespace Integration;

// test if sessions can be created and joined without error

public class SimpleSession
{

    // create server authoritative session with 3 clients
    [Fact]
    public void ServerAuthoritative()
    {
        Logs.InitPath("logs/SimpleSession/ServerAuth");
        Logs.InitFiles(
            "logs/SimpleSession/ServerAuth/server.log",
            "logs/SimpleSession/ServerAuth/client1.log",
            "logs/SimpleSession/ServerAuth/client2.log",
            "logs/SimpleSession/ServerAuth/client3.log"
        );

        var server = new Connection(new Connection.Args{
            role = NetRole.Server,
            serverAddr = "127.0.0.1",
            udpPort = 0,
            tcpPort = 0,
            logger = (str) => File.AppendAllTextAsync("logs/SimpleSession/ServerAuth/server.log", str),
            verbosity = Logger.Includes().All()
        });

        var client1 = new Connection(new Connection.Args{
            role = NetRole.Client,
            serverAddr = "127.0.0.1",
            udpPort = server.ServerUdpPort,
            tcpPort = server.ServerTcpPort,
            logger = (str) => File.AppendAllTextAsync("logs/SimpleSession/ServerAuth/client1.log", str),
            verbosity = Logger.Includes().All()
        });

        var client2 = new Connection(new Connection.Args{
            role = NetRole.Client,
            serverAddr = "127.0.0.1",
            udpPort = server.ServerUdpPort,
            tcpPort = server.ServerTcpPort,
            logger = (str) => File.AppendAllTextAsync("logs/SimpleSession/ServerAuth/client2.log", str),
            verbosity = Logger.Includes().All()
        });

        var client3 = new Connection(new Connection.Args{
            role = NetRole.Client,
            serverAddr = "127.0.0.1",
            udpPort = server.ServerUdpPort,
            tcpPort = server.ServerTcpPort,
            logger = (str) => File.AppendAllTextAsync("logs/SimpleSession/ServerAuth/client3.log", str),
            verbosity = Logger.Includes().All()
        });

        while (client3.ClientCount < 3)
        {
            server.ExecuteQueue();
            client1.ExecuteQueue();
            client2.ExecuteQueue();
            client3.ExecuteQueue();
            Thread.Sleep(20);
        }

        Assert.True(server.ClientCount == 3, $"server counts {server.ClientCount} clients, not 3. Clients connected: {server.Clients.Select(id => id.ToString()).Aggregate((str, id) => str + ", " + id)}");

        server.Disconnect();

        int iters = 10;
        while ((client1.IsActive || client2.IsActive || client3.IsActive) && iters > 0)
        {
            iters--;
            client1.ExecuteQueue();
            client2.ExecuteQueue();
            client3.ExecuteQueue();
            Thread.Sleep(20);
        }

        Assert.True(iters > 0, "Server disconnected, but clients took too long to register as disconnected.");
    }

    // create relayed session with 3 clients
    [Fact]
    public void RelaySession()
    {
        Logs.InitPath("logs/SimpleSession/Relayed");
        Logs.InitFiles(
            "logs/SimpleSession/Relayed/server.log",
            "logs/SimpleSession/Relayed/client1.log",
            "logs/SimpleSession/Relayed/client2.log",
            "logs/SimpleSession/Relayed/client3.log"
        );

        var server = new Connection(new Connection.Args{
            role = NetRole.Relay,
            serverAddr = "127.0.0.1",
            udpPort = 0,
            tcpPort = 0,
            logger = (str) => File.AppendAllTextAsync("logs/SimpleSession/Relayed/server.log", str),
            verbosity = Logger.Includes().All()
        });

        var client1 = new Connection(new Connection.Args{
            role = NetRole.Client,
            serverAddr = "127.0.0.1",
            udpPort = server.ServerUdpPort,
            tcpPort = server.ServerTcpPort,
            logger = (str) => File.AppendAllTextAsync("logs/SimpleSession/Relayed/client1.log", str),
            verbosity = Logger.Includes().All()
        });

        var client2 = new Connection(new Connection.Args{
            role = NetRole.Client,
            serverAddr = "127.0.0.1",
            udpPort = server.ServerUdpPort,
            tcpPort = server.ServerTcpPort,
            logger = (str) => File.AppendAllTextAsync("logs/SimpleSession/Relayed/client2.log", str),
            verbosity = Logger.Includes().All()
        });

        var client3 = new Connection(new Connection.Args{
            role = NetRole.Client,
            serverAddr = "127.0.0.1",
            udpPort = server.ServerUdpPort,
            tcpPort = server.ServerTcpPort,
            logger = (str) => File.AppendAllTextAsync("logs/SimpleSession/Relayed/client3.log", str),
            verbosity = Logger.Includes().All()
        });

        while (client3.ClientCount < 3)
        {
            server.ExecuteQueue();
            client1.ExecuteQueue();
            client2.ExecuteQueue();
            client3.ExecuteQueue();
            Thread.Sleep(20);
        }

        Assert.True(server.ClientCount == 3, $"server counts {server.ClientCount} clients, not 3. Clients connected: {server.Clients.Select(id => id.ToString()).Aggregate((str, id) => str + ", " + id)}");

        server.Disconnect();

        int iters = 10;
        while ((client1.IsActive || client2.IsActive || client3.IsActive) && iters > 0)
        {
            iters--;
            client1.ExecuteQueue();
            client2.ExecuteQueue();
            client3.ExecuteQueue();
            Thread.Sleep(20);
        }

        Assert.True(iters > 0, "Server disconnected, but clients took too long to register as disconnected.");
    }
}
