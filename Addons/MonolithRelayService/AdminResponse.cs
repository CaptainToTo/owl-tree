
using System.Text.Json;

public enum AdminResponseCodes
{
    Invalid = 0,

    LoginSuccess = 200,
    RelayListSuccess = 201,
    RelayDetailsSuccess = 202,
    
    IncorrectCredentials = 402,
    NotFound = 404,
    RequestRejected = 410
}

public struct LoginResponse
{
    public bool accepted { get; set; }

    public string Serialize()
    {
        return JsonSerializer.Serialize(this);
    }

    public static LoginResponse Deserialize(string data)
    {
        return JsonSerializer.Deserialize<LoginResponse>(data);
    }
}

public struct SessionDetails
{
    public string sessionId { get; set; }
    public string appId { get; set; }
    public string ipAddr { get; set; }
    public int tcpPort { get; set; }
    public int udpPort { get; set; }
    public int clientCount { get; set; }
    public int maxClients { get; set; }
    public uint authority { get; set; }
}

public struct SessionListResponse
{
    public AdminResponseCodes responseCode;

    public SessionDetails[] sessions { get; set; }

    public string Serialize()
    {
        return JsonSerializer.Serialize(this);
    }

    public static SessionListResponse Deserialize(string data)
    {
        return JsonSerializer.Deserialize<SessionListResponse>(data);
    }
}

public struct ClientData
{
    public uint clientId { get; set; }
    public int ping { get; set; }
}

public enum ClientEventType
{
    ClientConnection,
    ClientDisconnection,
    HostMigration
}

public struct ClientEvent
{
    public ClientEventType eventType { get; set; }
    public uint clientId { get; set; }
    public long timestamp { get; set; }
}

public struct BandwidthData
{
    public int[] recv { get; set; }
    public int[] send { get; set; }
}

public struct SessionDetailsResponse
{
    public AdminResponseCodes responseCode;

    public string sessionId { get; set; }
    public string appId { get; set; }
    public string ipAddr { get; set; }
    public int tcpPort { get; set; }
    public int udpPort { get; set; }
    public int clientCount { get; set; }
    public int maxClients { get; set; }
    public uint authority { get; set; }

    public ClientData[] clients { get; set; }
    public ClientEvent[] clientEvents { get; set; }

    public BandwidthData bandwidth { get; set; }
    
    public string logs { get; set; }

    public string Serialize()
    {
        return JsonSerializer.Serialize(this);
    }

    public static SessionDetailsResponse Deserialize(string data)
    {
        return JsonSerializer.Deserialize<SessionDetailsResponse>(data);
    }

    public static SessionDetailsResponse NotFound = new SessionDetailsResponse{
        responseCode = AdminResponseCodes.NotFound
    };
}