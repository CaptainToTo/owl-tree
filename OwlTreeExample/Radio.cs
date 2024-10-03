using OwlTree;

public class Radio : NetworkObject
{
    public override string ToString()
    {
        return "Radio No. " + Id.ToString();
    }

    int pingNo = 0;

    [Rpc(RpcCaller.Client)]
    public void RPC_PingServer(string message, [RpcCaller] ClientId caller = default)
    {
        Console.WriteLine("Message from client " + caller.ToString() + ":\n" + message);
        pingNo++;
        RPC_PingClient(caller, "hello from server: " + pingNo);
        if (pingNo > 50)
        {
            Connection?.Disconnect(caller);
            pingNo = 0;
        }
    }

    [Rpc(RpcCaller.Server)]
    public void RPC_PingClients(string message)
    {
        Console.WriteLine("Message from server:\n   " + message);
        pingNo++;
        RPC_PingServer("hello from client: " + pingNo);
    }

    [Rpc(RpcCaller.Server, Key = "PingClient")]
    public void RPC_PingClient([RpcCallee] ClientId callee, string message)
    {
        Console.WriteLine("Private message from server:\n   " + message);
        pingNo++;
        RPC_PingServer("hello from client: " + pingNo);
    }

    [Rpc(RpcCaller.Server)]
    public void RPC_SendNums(int i, float x, double z, byte j)
    {
        Console.WriteLine($"   {i}, {x}, {z}, {j}");
    }

    [Rpc(RpcCaller.Server)]
    public void RPC_Test()
    {
        Console.WriteLine("Received test");
    }
}