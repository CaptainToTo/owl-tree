using System.Collections.Concurrent;
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
    public static RelayManager relays;
    public static string ip = "127.0.0.1";

    public static void Main(string[] args)
    {
        var ip = args.Length > 1 ? args[0] : "127.0.0.1";
        var domain = args.Length > 2 ? args[1] : "http://127.0.0.1:5000/";
        var adminDomain = args.Length > 3 ? args[2] : "https://127.0.0.1:5001/";
        var username = args.Length > 4 ? args[3] : "127.0.0.1:5001";
        var password = args.Length > 5 ? args[4] : "OwlTreeAdmin";

        if (!Directory.Exists("logs"))
            Directory.CreateDirectory("logs");

        var endpoint = new MatchmakingEndpoint(domain, MatchmakingRequestCallbacks.HandleRequest);
        var admin = new AdminEndpoint(adminDomain, username, password);
        admin.OnSessionListRequest = AdminRequestCallbacks.HandleSessionListRequest;
        admin.OnSessionDetailsRequest = AdminRequestCallbacks.HandleSessionDetailsRequest;
        relays = new RelayManager();
        
        admin.Start();
        endpoint.Start();
        HandleCommands();
        endpoint.Close();
        admin.Close();
        relays.DisconnectAll();
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