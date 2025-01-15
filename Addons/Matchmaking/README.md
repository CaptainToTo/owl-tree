# OwlTree.Matchmaking

Provides a basic matchmaking service that can be used to collect necessary information to create connections.

## In the server application:

Open a `MatchmakingEndpoint`, and provide a request handler:

```cs
// manages existing sessions on this server
Dictionary<string, Connection> connections = new();

...

var endpoint = new MatchmakingEndpoint(
    "http://localhost:3000", // replace with server's URL
    HandleRequest
);
endpoint.Start();

...

// handle requests to create and join relayed sessions
MatchmakingResponse HandleRequest(MatchmakingRequest request)
{
    Connection connection = null;

    if (!connections.ContainsKey(request.sessionId))
    {
        // reject request if the client is looking for an existing session
        if (request.clientRole == ClientRole.Client)
            return MatchmakingResponse.RequestRejected;
        
        // create a new relay and send it's info to the host
        connection = new Connection( new Connection.Args{
            appId = request.appId,
            sessionId = request.sessionId,
            role = Connection.Role.Relay,
            // port no. of 0 will select any available port
            tcpPort = 0,
            udpPort = 0,
            maxClients = request.maxClients,
            migratable = request.migratable,
            owlTreeVersion = request.owlTreeVersion,
            minOwlTreeVersion = request.minOwlTreeVersion,
            appVersion = request.appVersion,
            minAppVersion = request.minAppVersion
        });
        connections.Add(connection.SessionId, connection);
    }
    // reject if the session already exists
    else if (request.clientRole == ClientRole.Host)
        return MatchmakingResponse.RequestRejected;
    // otherwise send the existing session to the client
    else
        connection = connections[request.sessionId];

    return new MatchmakingResponse{
        responseCode = ResponseCodes.RequestAccepted,
        serverAddr = "127.0.0.1", // replace with public IP address
        udpPort = connection.ServerUdpPort,
        tcpPort = connection.ServerTcpPort,
        sessionId = request.sessionId,
        appId = request.appId,
        serverType = ServerType.Relay
    };
}
```

## In the client application:

Create a new `MatchmakingClient`, send a request, and await a response:

```cs
var requestClient = new MatchmakingClient("http://localhost:3000");

var response = await requestClient.MakeRequest(new MatchmakingRequest{
    appId = "MyOwlTreeApp",
    sessionId = "MyOwlTreeAppSession",
    serverType = ServerType.Relay,
    ClientRole = ClientRole.Host,
    maxClients = 10,
    migratable = false,
    owlTreeVersion = 1,
    appVersion = 1
});

if (response.RequestFailed)
    Environment.Exit(0);

var connection = new Connection(new Connection.Args{
    role = NetRole.Host,
    serverAddr = response.serverAddr,
    tcpPort = response.tcpPort,
    udpPort = response.udpPort,
    appId = response.appId,
    sessionId = response.sessionId
});
```