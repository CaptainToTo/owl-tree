# Owl Tree (WIP)
A C++ and C# framework for server-client RPCs intended for games.

Select either the `cpp` or `cs` branch to view the source code.

# C# 

The C# version of Owl Tree is developed with .NET 8.0.\
The actual framework is contained in the `src` folder.\
If you want to use the framework, copy the contents of the `src` folder into your project.

Owl Tree C# require [PostSharp](https://www.postsharp.net/il). Add with:

```
> dotnet add package PostSharp --version 2024.1.5
```

# Creating a Connection

Owl Tree's main interface is the `Connection` class. To open a server, or connect as a client, create a new `Connection`:

```
using OwlTree;

static void Main(string[] args)
{
    if (args[0] == "server")
    {
        // Open server on localhost
        var server = new Connection(new Connection.Args
            {
                role = Connection.Role.Server,
                serverAddr = "127.0.0.1",
                port = 8080
            });
    }
    else if (args[0] == "client")
    {
        // create a client
        var client = new Connection(new Connection.Args
            {
                role = Connection.Role.Client,
                serverAddr = "127.0.0.1",
                port 8080
            });
        // synchronously wait for server to confirm connection
        client.AwaitConnection();
    }
}
```

Connections can be configured with the `Args` struct passed to the constructor.