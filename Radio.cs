using OwlTree;

public class Radio : NetworkObject
{
    public override string ToString()
    {
        return "Radio No. " + Id.ToString();
    }

    [Rpc(RpcPerms.Client), RpcHash("PingServer")]
    public void PingServer(string message, [RpcCaller] ClientId caller = default)
    {
        Console.WriteLine("Message from client " + caller.ToString() + ":\n" + message);
    }

    [Rpc(RpcPerms.Server), RpcHash("PingClients")]
    public void PingClients(string message)
    {
        Console.WriteLine("Message from server:\n" + message);
    }

    [Rpc(RpcPerms.Server), RpcHash("PingClient")]
    public void PingClient([RpcCallee] ClientId callee, string message)
    {
        Console.WriteLine("Private message from server: " + message);
    }
}