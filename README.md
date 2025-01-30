# OwlTree (v0.2.0)
A C# framework for server-client RPCs intended for games.

View the full documentation on the [wiki](https://github.com/CaptainToTo/owl-tree/wiki).

Or check out example projects here:
- [Checkers Club (.NET CLI checkers server)](https://github.com/CaptainToTo/checkers-club)
- [Farming With Friends 2 (Unity multiplayer farming game)](https://github.com/CaptainToTo/Farming-With-Friends-2)
- DrawDot (Godot drawing app) - under construction

# C# 

OwlTree uses .net standard 2.1, with the goal of being as engine/runtime agnostic as possible.\
Feel free to use it in anything you like. :)

# Setting Up

To start using OwlTree, you can download the latest release [here](https://github.com/CaptainToTo/owl-tree/releases/download/v0.2.0/OwlTree-v0.2.0.zip). This contains both the OwlTree project, and the OwlTree source generator. Include both in your project's .csproj file:

```xml
<ItemGroup>
    <ProjectReference Include="../OwlTree/OwlTree.csproj" />
    <Analyzer Include="../OwlTree.Generator/OwlTree.Generator.dll" />
</ItemGroup>
```

and enable source generators:

```xml
<PropertyGroup>
    <EnableSourceGenerators>true</EnableSourceGenerators>
</PropertyGroup>
```

See specific set-up procedures for other environments:
- [Unity](https://github.com/CaptainToTo/owl-tree-unity/wiki)
- Godot - under construction

# Creating a Connection

OwlTree's main interface is the `Connection` class. OwlTree supports both server authoritative, and relayed peer-to-peer architecture. The following example shows how to create a server authoritative session.

To open a server, or connect as a client, create a new `Connection`:

```cs
using OwlTree;

class Program
{
    static void Main(string[] args)
    {
        if (args[0] == "server")
        {
            // Open server on localhost
            var server = new Connection(new Connection.Args
            {
                appId = "MyOwlTreeApp", // max 64 ASCII character identifier for your app
                sessionId = "MyAppSessionId", // max 64 ASCII character identifier for a session of your app
                role = Connection.Role.Server,
                serverAddr = "127.0.0.1",
                tcpPort = 8000,
                udpPort = 9000,
            });
            server.OnClientConnected += (ClientId id) => 
                Console.WriteLine("new client w/ id: " + id);
            UpdateLoop(server);
        }
        else if (args[0] == "client")
        {
            // create a client
            var client = new Connection(new Connection.Args
            {
                appId = "MyOwlTreeApp", // if app id doesn't match server's id, connection will be rejected
                sessionId = "MyAppSessionId", // if session id doesn't match server's id, connection will be rejected
                role = Connection.Role.Client,
                serverAddr = "127.0.0.1",
                tcpPort = 8000,
                udpPort = 9000
            });
            client.OnReady += (ClientId localId) => 
                Console.WriteLine("connected to server, assigned id: " + localId);
            UpdateLoop(client);
        }
    }

    static void UpdateLoop(Connection connection)
    {
        while (connection.IsActive) // exit loop once connection is shut down
        {
            connection.ExecuteQueue(); // execute any incoming RPCs
            Thread.Sleep(100);
        }
    }
}
```

Connections can be configured with the `Args` object passed to the constructor. There can be multiple connections running at the same time in the same program, and will each manage their own state independently of each other. The primary way Connections manage state is through `NetworkObject`s, which allow you to create remote procedure calls (RPCs).

In the below example, a Radio class can be used to repeatedly send a small message back-and-forth between a client and a server. RPCs must be public, virtual, and cannot have a return type. RPCs are marked using the `Rpc` attribute. Optionally, the `Rpc` can be passed a `RpcPerms` argument that specifies what type of connections are allowed to call it. If no argument is given, or the `RpcPerms.AnyToAll` value is given, then either server or client can call it. Using the `CallerId` parameter attribute exposes which client called the RPC. The `CalleeId` parameter attribute allows you to specify a single client to receive an RPC call, while the others do not.

```cs
using OwlTree;

public class Radio : NetworkObject
{
    // rpc can only be called by clients
    [Rpc(RpcCaller.ClientsToAuthority)]
    public virtual void PingServer(string message, [CallerId] ClientId client = default)
    {
        Console.WriteLine("message from " + client + ": " + message);
        PingClient(client, "hello from server");
    }

    // rpc can only be called by servers
    [Rpc(RpcCaller.AuthorityToClients)]
    public virtual void PingClient([CalleeId] ClientId targetClient, string message)
    {
        Console.WriteLine("message from server: " + message);
        PingServer("hello from client " + Connection.LocalId);
    }
}
```

To create a new NetworkObject, use the `Spawn` method on your connection. Only server connections are allowed to call `Spawn`.

```cs
Radio myRadio = server.Spawn<Radio>();
myRadio.PingClient("first hello from server");

Radio clientRadio = client.Spawn<Radio>(); // ERROR! clients cannot spawn
```

Connections will automatically synchronize object spawning across clients. To be notified of when a client receives a new spawn, subscribe to the `OnObjectSpawn` event.

```cs
Radio myRadio;
client.OnObjectSpawn += (NetworkObject obj) => {
    if (obj is Radio)
        myRadio = (Radio)obj;
};
```

NetworkObjects can also be retrieved by their `NetworkId`:

```cs
NetworkId id;
client.OnObjectSpawn += (NetworkObject obj) => id = obj.Id;
...
Radio myRadio = (Radio)client.GetNetworkObject(id);
```

To remove a NetworkObject, call the `Despawn` method on the server:

```cs
server.Despawn(myRadio);
```

And subscribe to the corresponding event on the client:

```cs
client.OnObjectDespawn += (NetworkObject obj) => {
    if (obj == myRadio)
        myRadio = null;
}
```

NetworkObjects also contain callbacks for when they are spawned, and despawned:

```cs
using OwlTree;

public class Radio : NetworkObject
{
    // invoked on both server and client, before the Connection.OnObjectSpawn event is invoked
    public override void OnSpawn()
    {
        Console.WriteLine("hello!");
    }

    // invoked on both server and client, before the Connection.OnObjectDespawn event is invoked
    public override void OnDespawn()
    {
        Console.WriteLine("goodbye!");
    }
}
```

To close the local connection call `Disconnect`.

```cs
client.Disconnect(); // disconnect from server
server.Disconnect(); // shutdown server
```
