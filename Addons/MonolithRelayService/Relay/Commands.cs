
using OwlTree;

public static class Commands
{
    public static void Help()
    {
        Console.WriteLine("Relay server commands:");
        Console.WriteLine("  (h)elp:    prints this command list");
        Console.WriteLine("  (r)elays:  prints a list of relay servers currently running");
        Console.WriteLine("  (q)uit:    shutdown the relay server");
        Console.WriteLine("  (p)layers [session id]: prints a list of players currently on the server");
        Console.WriteLine("  ping [session id] [client id]: ping a client to test their latency");
        Console.WriteLine("  (d)isconnect [session id] [client id]: disconnect a client from the server");
    }

    public static void PlayerList(Connection relay)
    {
        if (relay.ClientCount == 0)
        {
            Console.WriteLine("no players connected");
            return;
        }

        Console.WriteLine("players:");
        foreach (var player in relay.Clients)
        {
            Console.WriteLine($"  {player} {(player == relay.Authority ? "[host]" : "")}");
        }
    }

    public static void Ping(string id, Connection relay)
    {
        if (!uint.TryParse(id, out var result))
        {
            Console.WriteLine("  ping failed...");
            return;
        }
        var clientId = new ClientId(result);
        if (!relay.ContainsClient(clientId))
        {
            Console.WriteLine("  ping failed...");
            return;
        }

        var ping = relay.Ping(clientId);

        Console.WriteLine($"  pinging {clientId}...");

        while (!ping.Resolved)
        {
            relay.ExecuteQueue();
        }

        if (ping.Failed)
        {
            Console.WriteLine("  ping failed...");
        }
        else
        {
            Console.WriteLine($"  {clientId} ping: {ping.Ping} ms");
        }
    }

    public static void Disconnect(string id, Connection relay)
    {
        if (!uint.TryParse(id, out var result))
        {
            Console.WriteLine("  disconnect failed...");
            return;
        }
        var clientId = new ClientId(result);
        if (!relay.ContainsClient(clientId))
        {
            Console.WriteLine("  disconnect failed...");
            return;
        }

        relay.Disconnect(clientId);
        Console.WriteLine("disconnecting " + clientId);
    }

    internal static void RelayList(RelayManager? relays)
    {
        if (relays == null) return;
        Console.WriteLine("Relays:");
        foreach (var relay in relays.Connections)
            Console.WriteLine($"   {(relay.IsRelay ? "relay" : "server")} {relay.SessionId} ({relay.AppId}): TCP: {relay.ServerTcpPort}, UDP: {relay.ServerUdpPort}, {relay.ClientCount}/{relay.MaxClients} clients");
    }
}