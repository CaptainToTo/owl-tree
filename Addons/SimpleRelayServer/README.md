# OwlTree Simple Relay Server

A persistent singular relay server that can be used for simple, local testing. The server will not shutdown when empty,
so it is perfect for short, frequent test sessions.

In the Relay folder, open a terminal, and run:
```
> dotnet run [app id] [session id] [tcp port] [udp port]
```

All arguments are optional, defaults are:
- app id: "MyOwlTreeApp"
- session id: "MyAppSession"
- tcp port: 8000
- udp port: 9000

The server will immediate start accepting clients. You can control the server using CLI commands:

```
Relay Server Commands:
  (h)elp:    prints this command list
  (p)layers: prints a list of players currently on the server
  (q)uit:    shutdown the relay server
  ports:     display the TCP and UDP ports the server is using
  ping [client id]: ping a client to test their latency
  (d)isconnect [client id]: disconnect a client from the server
```

The program will assign itself a random number id which will be associated with a log file it will output to.