using OwlTree;
using OwlTree.Matchmaking;

/// Example test client to use test relay service

public static class Program
{
    public static void Main(string[] args)
    {
        if (!Directory.Exists("logs"))
            Directory.CreateDirectory("logs");

        if (args.Length == 3)
            UseMatchmaking(args);
        else if (args.Length == 5)
            DirectConnect(args);
        else
        {
            Console.WriteLine("Matchmaking Usage: dotnet run [appId] [endpoint] [client/host]");
            Console.WriteLine("Direct Connect Usage: dotnet run [appId] [sessionId] [ip address] [tcp] [udp]");
        }
    }

    private static void DirectConnect(string[] args)
    {
        var rand = new Random();
        var logId = (rand.Next() % 900) + 100;
        var logFile = $"logs/client{logId}.log";
        Console.WriteLine("client log id: " + logId.ToString());

        var client = new Connection(new Connection.Args{
            role = NetRole.Client,
            serverAddr = args[2],
            tcpPort = int.Parse(args[3]),
            udpPort = int.Parse(args[4]),
            appId = args[0],
            sessionId = args[1],
            logger = (str) => File.AppendAllText(logFile, str),
            verbosity = Logger.Includes().All()
        });

        client.OnReady += (id) => {
            if (client.IsHost)
                Console.WriteLine("assigned as host");
            Console.WriteLine("assigned client id: " + id.ToString());
        };

        client.OnClientConnected += (id) => {
            Console.WriteLine($"client {id} connected");
        };

        client.OnHostMigration += (id) => {
            if (client.IsHost)
                Console.WriteLine("you are now the host");
            else
                Console.WriteLine($"client {id} assigned as new host");
        };

        client.OnLocalDisconnect += (id) => {
            Console.WriteLine("disconnected");
        };

        while (client.IsActive)
        {
            client.ExecuteQueue();
            Thread.Sleep(5);
        }

        client.Disconnect();
    }

    private static void UseMatchmaking(string[] args)
    {
        var rand = new Random();
        var logId = (rand.Next() % 900) + 100;
        var logFile = $"logs/client{logId}.log";
        Console.WriteLine("client log id: " + logId.ToString());

        var request = new MatchmakingClient("http://" + args[1]);
        var promise = request.MakeRequest(new MatchmakingRequest{
            appId = args[0],
            sessionId = logId.ToString(),
            serverType = ServerType.Relay,
            clientRole = args[2] == "host" ? ClientRole.Host : ClientRole.Client,
            maxClients = 6,
            migratable = true,
            owlTreeVersion = 1,
            appVersion = 1
        });

        while (!promise.IsCompleted)
            Thread.Sleep(50);
        
        if (promise.IsFaulted)
            return;
        
        var response = promise.Result;

        if (response.RequestFailed)
        {
            Console.WriteLine("failed to request relay server");
            Environment.Exit(0);
        }

        var client = new Connection(new Connection.Args{
            role = NetRole.Client,
            serverAddr = response.serverAddr,
            tcpPort = response.tcpPort,
            udpPort = response.udpPort,
            appId = response.appId,
            sessionId = response.sessionId,
            logger = (str) => File.AppendAllText(logFile, str),
            verbosity = Logger.Includes().All()
        });

        client.OnReady += (id) => {
            if (client.IsHost)
                Console.WriteLine("assigned as host");
            Console.WriteLine("assigned client id: " + id.ToString());
        };

        client.OnClientConnected += (id) => {
            Console.WriteLine($"client {id} connected");
        };

        client.OnHostMigration += (id) => {
            if (client.IsHost)
                Console.WriteLine("you are now the host");
            else
                Console.WriteLine($"client {id} assigned as new host");
        };

        client.OnLocalDisconnect += (id) => {
            Console.WriteLine("disconnected");
        };

        while (client.IsActive)
        {
            client.ExecuteQueue();
            Thread.Sleep(5);
        }

        client.Disconnect();
    }
}
