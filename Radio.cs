using OwlTree;

public class Radio : NetworkObject
{
    public override string ToString()
    {
        return "Radio No. " + Id.ToString();
    }

    [Rpc(RpcCaller.Client)]
    public void PingServer(string message)
    {
        Console.WriteLine("Message from client:\n" + message);
    }

    [Rpc(RpcCaller.Server)]
    public void PingClients(string message)
    {
        Console.WriteLine("Message from server:\n" + message);
    }
}