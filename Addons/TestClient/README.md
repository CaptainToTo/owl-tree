# OwlTree Simple Test Client

A CLI client to test the provided relay servers.

## Usage:

To test a simple relay server:
```
> dotnet run [appId] [sessionId] [ip address] [tcp port] [udp port]

> dotnet run MyAppId MyAppSession 127.0.0.1 8000 9000
```

To test a relay service:
```
> dotnet run [appId] [endpoint] [client/host] [sessionId]

> dotnet run MyAppId http://my-endpoint.com host
> dotnet run MyAppId http://my-endpoint.com client MyAppSession
```

Clients will output logs to file with an assigned 3 digit id. This will also be the session id requested
if connecting to a relay service.