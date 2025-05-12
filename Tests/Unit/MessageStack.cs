using FileInitializer;
using OwlTree;

namespace Unit;

public class MessageStackTests
{
    [Fact]
    public void PushAndPop()
    {
        var stack = new MessageStack(10);

        stack.Push(new IncomingMessage{
            tick = new Tick(1),
            rpcId = new RpcId(3)
        });
        stack.Push(new IncomingMessage{
            tick = new Tick(1),
            rpcId = new RpcId(4)
        });
        stack.Push(new IncomingMessage{
            tick = new Tick(1),
            rpcId = new RpcId(5)
        });

        stack.Push(new IncomingMessage{
            tick = new Tick(2),
            rpcId = new RpcId(3)
        });
        stack.Push(new IncomingMessage{
            tick = new Tick(2),
            rpcId = new RpcId(4)
        });
        stack.Push(new IncomingMessage{
            tick = new Tick(2),
            rpcId = new RpcId(5)
        });

        stack.Push(new IncomingMessage{
            tick = new Tick(3),
            rpcId = new RpcId(3)
        });
        stack.Push(new IncomingMessage{
            tick = new Tick(3),
            rpcId = new RpcId(4)
        });
        stack.Push(new IncomingMessage{
            tick = new Tick(3),
            rpcId = new RpcId(5)
        });

        var start = stack.GetTickStart(new Tick(2), out var ind);

        Assert.True(start != null, "failed to get the start of tick 2, ind returned was " + ind);

        Assert.True(start.Value.rpcId == 3, "start of tick 2 is not rpc 3, instead got " + start.Value.rpcId);

        int i = 0;
        foreach (var m in stack.RewindFrom(new Tick(2)))
        {
            i++;
        }

        Assert.True(i == 6, "6 messages were not popped, instead got " + i + ",\n" + stack.ToString());
    }
}