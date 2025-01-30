using System.Net;
using OwlTree;
using OwlTree.Matchmaking;

public static class MatchmakingRequestCallbacks
{
    // called asynchronously by matchmaking endpoint
    public static MatchmakingResponse HandleRequest(IPAddress client, MatchmakingRequest request)
    {
        if (Program.relays == null)
            return MatchmakingResponse.RequestRejected;
        
        Connection connection = null;

        // create a new relay if the the session id hasn't been taken yet
        if (!Program.relays.Contains(request.sessionId))
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

            connection = Program.relays.Add(new Connection.Args{
                appId = request.appId,
                sessionId = request.sessionId,
                role = NetRole.Relay,
                serverAddr = Program.ip,
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
            connection = Program.relays.Get(request.sessionId);
        
        if (connection == null)
            return MatchmakingResponse.RequestRejected;
        
        return new MatchmakingResponse{
            responseCode = ResponseCodes.RequestAccepted,
            serverAddr = Program.ip,
            udpPort = connection.ServerUdpPort,
            tcpPort = connection.ServerTcpPort,
            sessionId = request.sessionId,
            appId = request.appId,
            serverType = ServerType.Relay
        };
    }
}