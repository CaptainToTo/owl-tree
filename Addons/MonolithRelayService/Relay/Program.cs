using System.Net;
using OwlTree;
using OwlTree.Matchmaking;


/// <summary>
/// Program contains 3 primary threads. A main thread that handles CLI input.
/// An HTTP thread created by the matchmaking endpoint to handle requests.
/// And a relay thread created by the relay manager that will continuously execute queues.
/// Each relay also manages its own recv/send threads. Each relay will also produce log files
/// named after their session id.
/// </summary>

public static class Program
{
    public static RelayManager? relays;
    public static string ip = "127.0.0.1";

    public static void Main(string[] args)
    {
        var ip = args.Length > 1 ? args[0] : "127.0.0.1";
        var domain = args.Length > 2 ? args[1] : "http://127.0.0.1:5000/";

        if (!Directory.Exists("logs"))
            Directory.CreateDirectory("logs");

        var endpoint = new MatchmakingEndpoint(domain, HandleRequest);
        relays = new RelayManager();
        endpoint.Start();
        HandleCommands();
        endpoint.Close();
        relays.DisconnectAll();
    }

    // called asynchronously by matchmaking endpoint
    public static MatchmakingResponse HandleRequest(IPAddress client, MatchmakingRequest request)
    {
        if (relays == null)
            return MatchmakingResponse.RequestRejected;
        
        Connection? connection = null;

        // create a new relay if the the session id hasn't been taken yet
        if (!relays.Contains(request.sessionId))
        {
            if (request.clientRole != ClientRole.Host)
                return MatchmakingResponse.RequestRejected;

            // log file matching the session id
            var logFile = $"logs/relay{request.sessionId}.log";
            File.WriteAllText(logFile, "");

            connection = relays.Add(new Connection.Args{
                appId = request.appId,
                sessionId = request.sessionId,
                role = NetRole.Relay,
                serverAddr = ip,
                tcpPort = 0,
                udpPort = 0,
                hostAddr = client.ToString(),
                maxClients = request.maxClients,
                migratable = request.migratable,
                owlTreeVersion = request.owlTreeVersion,
                minOwlTreeVersion = request.minOwlTreeVersion,
                appVersion = request.appVersion,
                minAppVersion = request.minAppVersion,
                logger = (str) => File.AppendAllText(logFile, str),
                verbosity = Logger.Includes().All()
            });
            connection.Log($"New Relay: {connection.SessionId} for App {connection.AppId}\nTCP: {connection.LocalTcpPort}\nUDP: {connection.LocalUdpPort}\nRequested Host: {client}");
        }
        // reject if the session already exists
        else if (request.clientRole == ClientRole.Host)
            return MatchmakingResponse.RequestRejected;
        // otherwise send the existing session to the client
        else
            connection = relays.Get(request.sessionId);
        
        if (connection == null)
            return MatchmakingResponse.RequestRejected;
        
        return new MatchmakingResponse{
            responseCode = ResponseCodes.RequestAccepted,
            serverAddr = ip,
            udpPort = connection.ServerUdpPort,
            tcpPort = connection.ServerTcpPort,
            sessionId = request.sessionId,
            appId = request.appId,
            serverType = ServerType.Relay
        };
    }

    // main thread handles CLI
    public static void HandleCommands()
    {
        while (true)
        {
            Console.Write("input command (h): ");
            var com = Console.ReadLine();
            if (com == null)
                continue;

            var tokens = com.Split(' ');

            var quit = false;

            switch (tokens[0])
            {
                case "r":
                case "relays":
                    Commands.RelayList(relays);
                    break;
                case "p":
                case "players":
                    if (tokens.Length != 2)
                    {
                        Console.WriteLine("a session id must be provided\n");
                        break;
                    }
                    var relay = relays!.Get(tokens[1]);
                    if (relay == null)
                    {
                        Console.WriteLine("no relay has that session id\n");
                        break;
                    }
                    Commands.PlayerList(relay);
                    break;
                case "q":
                case "quit":
                    quit = true;
                    break;
                case "ping":
                    if (tokens.Length != 3)
                    {
                        Console.WriteLine("a session id and client id must be provided\n");
                        break;
                    }
                    relay = relays!.Get(tokens[1]);
                    if (relay == null)
                    {
                        Console.WriteLine("no relay has that session id\n");
                        break;
                    }
                    Commands.Ping(tokens[2], relay);
                    break;
                case "d":
                case "disconnect":
                    if (tokens.Length != 3)
                    {
                        Console.WriteLine("a session id and client id must be provided\n");
                        break;
                    }
                    relay = relays!.Get(tokens[1]);
                    if (relay == null)
                    {
                        Console.WriteLine("no relay has that session id\n");
                        break;
                    }
                    Commands.Disconnect(tokens[2], relays!.Get(tokens[1])!);
                    break;
                case "h":
                case "help":
                default:
                    Commands.Help();
                    break;
            }

            if (quit)
            {
                break;
            }
        }
    }
}

// var rand = new Random();
// var logId = rand.Next();

// var logFile = $"relay{logId}.log";
// Console.WriteLine("relay log id: " + logId.ToString());

// var relay = new Connection(new Connection.Args{
//     role = Connection.Role.Relay,
//     appId = "FarmingWithFriends_OwlTreeExample",
//     migratable = true,
//     shutdownWhenEmpty = false,
//     maxClients = 10,
//     useCompression = true,
//     printer = (str) => File.AppendAllText(logFile, str),
//     verbosity = Logger.Includes().All()
// });

// while (relay.IsActive)
// {
//     relay.ExecuteQueue();
//     Console.Write("relay command (h): ");
//     var com = Console.ReadLine();
//     if (com == null)
//         continue;

//     var tokens = com.Split(' ');

//     var quit = false;

//     relay.ExecuteQueue();
//     switch (tokens[0])
//     {
//         case "p":
//         case "players":
//             Commands.PlayerList(relay);
//             break;
//         case "q":
//         case "quit":
//             quit = true;
//             break;
//         case "ping":
//             Commands.Ping(tokens[1], relay);
//             break;
//         case "d":
//         case "disconnect":
//             Commands.Disconnect(tokens[1], relay);
//             break;
//         case "h":
//         case "help":
//         default:
//             Commands.Help();
//             break;
//     }

//     if (quit)
//     {
//         relay.Disconnect();
//         break;
//     }
// }


