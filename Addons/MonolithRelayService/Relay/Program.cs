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

        var endpoint = new MatchmakingEndpoint(domain, HandleRequest);
        var admin = new AdminEndpoint(adminDomain, username, password);
        admin.OnSessionListRequest = HandleSessionListRequest;
        admin.OnSessionDetailsRequest = HandleSessionDetailsRequest;
        relays = new RelayManager();
        admin.Start();
        endpoint.Start();
        HandleCommands();
        endpoint.Close();
        admin.Close();
        relays.DisconnectAll();
    }

    private static SessionDetailsResponse HandleSessionDetailsRequest(string sessionId)
    {
        var relay = relays.Get(sessionId);

        if (relay == null)
            return SessionDetailsResponse.NotFound;
        
        var response = new SessionDetailsResponse{
            sessionId = relay.SessionId.Id,
            appId = relay.AppId,
            ipAddr = ip,
            tcpPort = relay.ServerTcpPort,
            udpPort = relay.ServerUdpPort,
            authority = relay.Authority.Id,
            maxClients = relay.MaxClients,
            clients = new ClientData[relay.ClientCount],
            logs = File.ReadAllText($"logs/relay{relay.SessionId.Id}.log")
        };

        var clients = relay.Clients.ToArray();
        int c = 0;
        foreach (var client in clients)
        {
            response.clients[c].clientId = client.Id;
            var ping = relay.Ping(client);
            while (!ping.Resolved)
                Thread.Sleep(50);
            response.clients[c].ping = ping.Ping;
        }

        var clientEvents = File.ReadAllText($"logs/relay{relay.SessionId.Id}-clients.log").Split('\n');
        response.clientEvents = new ClientEvent[clientEvents.Length - 1];
        for (int i = 0; i < clientEvents.Length - 1; i++)
        {
            var tokens = clientEvents[i].Split(' ');

            if (tokens[0] == "connected")
                response.clientEvents[i].eventType = ClientEventType.ClientConnection;
            else if (tokens[0] == "disconnected")
                response.clientEvents[i].eventType = ClientEventType.ClientDisconnection;
            else if (tokens[0] == "migrated")
                response.clientEvents[i].eventType = ClientEventType.HostMigration;
            
            response.clientEvents[i].clientId = uint.Parse(tokens[1]);
            response.clientEvents[i].timestamp = long.Parse(tokens[3]);
        }

        var bandwidthGroups = File.ReadLines($"logs/relay{relay.SessionId.Id}-bandwidth.log")
            .Select(l => {
                var tokens = l.Split(' ');
                return (tokens[0] == "send", int.Parse(tokens[1]), long.Parse(tokens[3]));
            })
            .GroupBy(a => a.Item3 / 1000);
        var recv = new List<int>();
        var send = new List<int>();

        foreach (var group in bandwidthGroups)
        {
            int sent = group.Where(a => a.Item1).Sum(a => a.Item2);
            int received = group.Where(a => !a.Item1).Sum(a => a.Item2);

            send.Add(sent);
            recv.Add(received);
        }

        response.bandwidth = new BandwidthData{
            send = send.ToArray(),
            recv = recv.ToArray()
        };

        response.responseCode = AdminResponseCodes.RelayDetailsSuccess;

        return response;
    }

    private static SessionListResponse HandleSessionListRequest()
    {
        var response = new SessionListResponse{
            sessions = new SessionDetails[relays.Count]
        };

        int i = 0;
        foreach (var relay in relays.Connections)
        {
            response.sessions[i].sessionId = relay.SessionId.Id;
            response.sessions[i].appId = relay.AppId.Id;
            response.sessions[i].tcpPort = relay.ServerTcpPort;
            response.sessions[i].udpPort = relay.ServerUdpPort;
            response.sessions[i].clientCount = relay.ClientCount;
            response.sessions[i].maxClients = relay.MaxClients;
            response.sessions[i].ipAddr = ip;
            response.sessions[i].authority = relay.Authority.Id;
            i++;
        }

        response.responseCode = AdminResponseCodes.RelayListSuccess;

        return response;
    }

    // called asynchronously by matchmaking endpoint
    public static MatchmakingResponse HandleRequest(IPAddress client, MatchmakingRequest request)
    {
        if (relays == null)
            return MatchmakingResponse.RequestRejected;
        
        Connection connection = null;

        // create a new relay if the the session id hasn't been taken yet
        if (!relays.Contains(request.sessionId))
        {
            if (request.clientRole != ClientRole.Host)
                return MatchmakingResponse.RequestRejected;

            // log file matching the session id
            var logFile = $"logs/relay{request.sessionId}.log";
            File.WriteAllText(logFile, "");

            var clientsFile = $"logs/relay{request.sessionId}-clients.log";
            File.WriteAllText(clientsFile, "");

            var bandwidthFile = $"logs/relay{request.sessionId}-bandwidth.log";
            File.WriteAllText(bandwidthFile, "");

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
                measureBandwidth = true,
                bandwidthReporter = (bandwidth) => {
                    var lastIncoming = bandwidth.LastIncoming();
                    var lastOutgoing = bandwidth.LastOutgoing();
                    if (lastIncoming.time > lastOutgoing.time)
                        File.AppendAllTextAsync(bandwidthFile, $"recv {lastIncoming.bytes} @ {lastIncoming.time}\n");
                    else
                        File.AppendAllTextAsync(bandwidthFile, $"send {lastOutgoing.bytes} @ {lastOutgoing.time}\n");
                },
                logger = (str) => File.AppendAllTextAsync(logFile, str),
                verbosity = Logger.Includes().All()
            });

            connection.OnClientConnected += (id) => File.AppendAllTextAsync(clientsFile, $"connected {id.Id} @ {DateTimeOffset.Now.ToUnixTimeSeconds()}\n");
            connection.OnClientDisconnected += (id) => File.AppendAllTextAsync(clientsFile, $"disconnected {id.Id} @ {DateTimeOffset.Now.ToUnixTimeSeconds()}\n");
            connection.OnHostMigration += (id) => File.AppendAllTextAsync(clientsFile, $"migrated {id.Id} @ {DateTimeOffset.Now.ToUnixTimeSeconds()}\n");

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