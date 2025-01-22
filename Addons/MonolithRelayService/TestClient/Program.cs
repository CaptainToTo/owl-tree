using OwlTree;
using OwlTree.Matchmaking;

/// Example test client to use test relay service

var rand = new Random();
var logId = rand.Next();
var logFile = $"client{logId}.log";
Console.WriteLine("client log id: " + logId.ToString());

var request = new MatchmakingClient("http://localhost:5000");
var response = await request.MakeRequest(new MatchmakingRequest{
    appId = "FarmingWithFriends_OwlTreeExample",
    sessionId = logId.ToString(),
    serverType = ServerType.Relay,
    clientRole = ClientRole.Host,
    maxClients = 6,
    migratable = true,
    owlTreeVersion = 1,
    appVersion = 1
});

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
