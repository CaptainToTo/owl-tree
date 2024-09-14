using OwlTree;

public class Radio : NetworkObject
{
    public override string ToString()
    {
        return "Radio No. " + Id.ToString();
    }

    [Rpc(RpcPerms.Client), RpcHash("PingServer")]
    public void RPC_PingServer(string message, [RpcCaller] ClientId caller = default)
    {
        Console.WriteLine("Message from client " + caller.ToString() + ":\n" + message);
    }

    [Rpc(RpcPerms.Server), RpcHash("PingClients")]
    public void RPC_PingClients(string message)
    {
        Console.WriteLine("Message from server:\n" + message);
    }

    [Rpc(RpcPerms.Server), RpcHash("PingClient")]
    public void RPC_PingClient([RpcCallee] ClientId callee, string message)
    {
        Console.WriteLine("Private message from server: " + message);
    }
}