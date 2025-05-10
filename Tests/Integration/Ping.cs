using System.Net.NetworkInformation;
using FileInitializer;
using OwlTree;

namespace Unit;

public class PingTests
{
    [Fact]
    public void PingNoOne()
    {
        Logs.InitPath("logs/Ping/NoTarget");
        Logs.InitFiles("logs/Ping/NoTarget/Server.log");

        var server = new Connection(new Connection.Args{
            logger = (str) => File.AppendAllText("logs/Ping/NoTarget/Server.log", str),
            verbosity = Logger.Includes().All()
        });

        var request = server.TestPing(new ClientId(1));

        int iters = 0;
        while (!request.Resolved && iters < 1000)
        {
            server.ExecuteQueue();
            Thread.Sleep(10);
            iters++;
        }

        server.Log(request.ToString());

        Assert.True(request.Resolved, $"ping was not resolved, meaning loop exited from emergency exit.");

        Assert.True(request.Failed, "request didn't fail, which should not be possible");

        Assert.True(iters <= 305, $"ping did not resolve in failure in ~3 seconds. Loop exited after {iters * 10} ms.");
    }

    [Fact]
    public void ToServer()
    {
        Logs.InitPath("logs/Ping/ToServer");
        Logs.InitFiles("logs/Ping/ToServer/client.log");
        Logs.InitFiles("logs/Ping/ToServer/server.log");
        Logs.InitFiles("logs/Ping/ToServer/ping.log");

        var server = new Connection(new Connection.Args{
            role = NetRole.Relay,
            tcpPort = 0,
            udpPort = 0,
            logger = (str) => File.AppendAllText("logs/Ping/ToServer/server.log", str),
            verbosity = Logger.Includes().All(),
            threadUpdateDelta = 20
        });

        var client = new Connection(new Connection.Args{
            role = NetRole.Client,
            appId = server.AppId,
            sessionId = server.SessionId,
            tcpPort = server.LocalTcpPort,
            udpPort = server.LocalUdpPort,
            logger = (str) => File.AppendAllText("logs/Ping/ToServer/client.log", str),
            verbosity = Logger.Includes().All(),
            threadUpdateDelta = 40
        });

        while (!client.IsReady)
        {
            client.ExecuteQueue();
            Thread.Sleep(20);
        }

        int avg = 0;

        for (int i = 0; i < 20; i++)
        {
            var request = client.Ping(ClientId.None);
            while (!request.Resolved)
            {
                client.ExecuteQueue();
                Thread.Sleep(20);
            }
            avg += request.Ping;

            File.AppendAllText("logs/Ping/ToServer/ping.log", 
                $"{(request.Failed ? "failed" : "ping")}: {request.Ping}, Recevied in: {request.TimeToTarget}ms, Responded in: {request.TimeToResponse}ms, Cllient latency: {client.Latency}ms, Server latency: {server.Latency}ms\n");
        }

        File.AppendAllText("logs/Ping/ToServer/ping.log", "Average ping: " + (avg / 20));

        client.Disconnect();
        server.Disconnect();
    }
}