# OwlTree Monolith Relay Service

A monolith relay solution that provides matchmaking and relay management. This service is built to run on a single instance. This is intended for simple testing, and low user counts.

## On The Server:

Build and run the relay service located in the Relay folder:
```
> dotnet build
> dotnet run [server ip] [matchmaking domain]
```

Optionally provide the public ip address the server will use, and the URL the matchmaking service will listen to.

If not provided the defaults are localhost:
- server ip: `127.0.0.1`
- matchmaking domain: `http://127.0.0.1:5000`

The server will immediately start processing matchmaking requests, and creating relay connections accordingly. You can control the server using CLI commands:

```
Relay server commands:
  (h)elp:    prints this command list
  (r)elays:  prints a list of relay servers currently running
  (q)uit:    shutdown the relay server
  (p)layers [session id]: prints a list of players currently on the server
  ping [session id] [client id]: ping a client to test their latency
  (d)isconnect [session id] [client id]: disconnect a client from the server
```

## On The Client:

The client can connect to the relay server by first sending a matchmaking request, and then connecting to the relay using data from the response.

```cs
var requestClient = new MatchmakingClient("http://127.0.0.1:5000"); // the URL used by the server

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
    serverAddr = response.serverAddr, // this will be the server ip given in the relay program args
    tcpPort = response.tcpPort,
    udpPort = response.udpPort,
    appId = response.appId,
    sessionId = response.sessionId
});
```
