public static class AdminRequestCallbacks
{
    public static SessionDetailsResponse HandleSessionDetailsRequest(string sessionId)
    {
        var relay = Program.relays.Get(sessionId);

        if (relay == null)
            return SessionDetailsResponse.NotFound;
        
        var response = new SessionDetailsResponse{
            sessionId = relay.SessionId.Id,
            appId = relay.AppId,
            ipAddr = Program.ip,
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

    public static SessionListResponse HandleSessionListRequest()
    {
        var response = new SessionListResponse{
            sessions = new SessionDetails[Program.relays.Count]
        };

        int i = 0;
        foreach (var relay in Program.relays.Connections)
        {
            response.sessions[i].sessionId = relay.SessionId.Id;
            response.sessions[i].appId = relay.AppId.Id;
            response.sessions[i].tcpPort = relay.ServerTcpPort;
            response.sessions[i].udpPort = relay.ServerUdpPort;
            response.sessions[i].clientCount = relay.ClientCount;
            response.sessions[i].maxClients = relay.MaxClients;
            response.sessions[i].ipAddr = Program.ip;
            response.sessions[i].authority = relay.Authority.Id;
            i++;
        }

        response.responseCode = AdminResponseCodes.RelayListSuccess;

        return response;
    }
}