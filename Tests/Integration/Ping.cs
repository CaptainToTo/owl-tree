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
        while (!request.Resolved && iters < 300)
        {
            server.ExecuteQueue();
            Thread.Sleep(10);
            iters++;
        }

        server.Log(request.ToString());

        Assert.True(request.Resolved, $"ping was not resolved, meaning loop exited from emergency exit.");

        Assert.True(request.Failed, "request didn't fail, which should not be possible");

        Assert.True(iters <= 300, $"ping did not resolve in failure in 3 second. Loop exited after {iters * 10} ms.");
    }
}