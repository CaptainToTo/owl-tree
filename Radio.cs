using OwlTree;

public class Radio : NetworkObject
{
    public override string ToString()
    {
        return "Radio No. " + Id.ToString();
    }

    [Rpc(RpcCaller.Client, InvokeOnCaller = true)]
    public void RPC_PingServer(string message, [RpcCaller] ClientId caller = default)
    {
        Console.WriteLine("Message from client " + caller.ToString() + ":\n" + message);
    }

    [Rpc(RpcCaller.Server)]
    public void RPC_PingClients(string message)
    {
        Console.WriteLine("Message from server:\n" + message);
        RPC_PingServer("hello from client");
    }

    [Rpc(RpcCaller.Server, Key = "PingClient")]
    public void RPC_PingClient([RpcCallee] ClientId callee, string message)
    {
        Console.WriteLine("Private message from server: " + message);
    }

    [Rpc(RpcCaller.Server)]
    public void RPC_SendNums(int i, float x, double z, byte j)
    {
        
    }
}