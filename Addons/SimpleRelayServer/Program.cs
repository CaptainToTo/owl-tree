using OwlTree;

public static class Program
{
    public static void Main(string[] args)
    {

        var appId = args.Length > 0 ? args[0] : "MyOwlTreeApp";
        var sessionId = args.Length > 1 ? args[1] : "MyAppSession";
        var tcpPort = args.Length > 2 ? int.Parse(args[2]) : 8000;
        var udpPort = args.Length > 3 ? int.Parse(args[3]) : 9000;

        var rand = new Random();
        var logId = rand.Next();

        if (!Directory.Exists("logs"))
            Directory.CreateDirectory("logs");

        var logFile = $"logs/relay{logId}.log";
        Console.WriteLine("relay log id: " + logId.ToString());

        var relay = new Connection(new Connection.Args{
            role = NetRole.Relay,
            appId = appId,
            sessionId = sessionId,
            tcpPort = tcpPort,
            udpPort = udpPort,
            migratable = true,
            shutdownWhenEmpty = false,
            maxClients = 10,
            useCompression = true,
            logger = (str) => File.AppendAllText(logFile, str),
            verbosity = Logger.Includes().All()
        });

        while (relay.IsActive)
        {
            relay.ExecuteQueue();
            Console.Write("relay command (h): ");
            var com = Console.ReadLine();
            if (com == null)
                continue;

            var tokens = com.Split(' ');

            var quit = false;

            relay.ExecuteQueue();
            switch (tokens[0])
            {
                case "p":
                case "players":
                    Commands.PlayerList(relay);
                    break;
                case "q":
                case "quit":
                    quit = true;
                    break;
                case "ping":
                    Commands.Ping(tokens[1], relay);
                    break;
                case "d":
                case "disconnect":
                    Commands.Disconnect(tokens[1], relay);
                    break;
                case "ports":
                    Console.WriteLine($"   TCP: {relay.ServerTcpPort}, UDP: {relay.ServerUdpPort}");
                    break;
                case "h":
                case "help":
                default:
                    Commands.Help();
                    break;
            }

            if (quit)
            {
                relay.Disconnect();
                break;
            }
        }
    }
}