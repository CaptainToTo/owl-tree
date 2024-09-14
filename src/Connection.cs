
namespace OwlTree
{
    /// <summary>
    /// Primary interface for OwlTree server-client connections. 
    /// </summary>
    public class Connection
    {
        /// <summary>
        /// Whether the Connection represents a server or client instance.
        /// </summary>
        public enum Role
        {
            Server,
            Client
        }

        /// <summary>
        /// Initialization arguments for building a new connection.
        /// </summary>
        public struct ConnectionArgs
        {
            /// <summary>
            /// Whether this connection is a server or client.<b>Default = Server</b>
            /// </summary>
            public Role role = Role.Server;
            /// <summary>
            /// The server IP address. <b>Default = localhost</b>
            /// </summary>
            public string serverAddr = "127.0.0.1";
            /// <summary>
            /// The server port. <b>Default = 8080</b>
            /// </summary>
            public int port = 8080;
            /// <summary>
            /// The maximum number of clients the server will allow to be connected at once.
            /// <b>Default = 4</b>
            /// </summary>
            public byte maxClients = 4;
            /// <summary>
            /// The byte length of read and write buffers. messages exceeding this length will cause data loss.
            /// <b>Default = 2048</b>
            /// </summary>
            public int bufferSize = 2048;

            public ConnectionArgs() { }
        }

        /// <summary>
        /// Create a new connection. Server instances will be immediately ready. 
        /// Clients will require an initial <c>StartConnection()</c> call.
        /// </summary>
        public Connection(ConnectionArgs args)
        {
            RpcAttribute.GenerateRpcProtocols();
            if (args.role == Role.Client)
            {
                _buffer = new ClientBuffer(args.serverAddr, args.port, args.bufferSize);
            }
            else
            {
                _buffer = new ServerBuffer(args.serverAddr, args.port, args.maxClients, args.bufferSize);
                IsActive = true;
            }
            role = args.role;
            _buffer.OnClientConnected = (id) => OnClientConnected?.Invoke(id);
            _buffer.OnClientDisconnected = (id) => {
                if (id == _buffer.LocalId)
                    IsActive = false;
                OnClientDisconnected?.Invoke(id);
            };
            _buffer.OnReady = (id) => {
                IsActive = true;
                OnReady?.Invoke(id);
            };

            _spawner = new NetworkSpawner(this, _buffer);

            _spawner.OnObjectSpawn = (obj) => OnObjectSpawn?.Invoke(obj);
            _spawner.OnObjectDestroy = (obj) => OnObjectDestroy?.Invoke(obj);
        }

        /// <summary>
        /// Whether this connection represents a server or client.
        /// </summary>
        public Role role { get; private set; }

        private NetworkBuffer _buffer;

        /// <summary>
        /// Invoked when a new client connects. Provides the id of the new client.
        /// </summary>
        public event ClientId.Delegate? OnClientConnected;

        /// <summary>
        /// Invoked when a client disconnects. Provides the id of the disconnected client.
        /// </summary>
        public event ClientId.Delegate? OnClientDisconnected;

        /// <summary>
        /// Invoked when the local connection is ready. On a server, provides <c>ClientId.None</c>.
        /// On a client, provides the local client id, as assigned by the server.
        /// </summary>
        public event ClientId.Delegate? OnReady;

        /// <summary>
        /// Whether this connection is active. Will be false for clients if they have been disconnected from the server.
        /// </summary>
        public bool IsActive { get; private set; } = false;

        /// <summary>
        /// The client id assigned to this local instance. Servers will have a LocalId of <c>ClientId.None</c>
        /// </summary>
        public ClientId LocalId { get { return _buffer.LocalId; } }

        /// <summary>
        /// Receive and execute any RPCs.
        /// </summary>
        public void Read()
        {
            _buffer.Read();

            while (GetNextMessage(out var message))
            {
                if (message.bytes == null) continue;

                if (
                    role == Role.Client && 
                    (message.bytes[0] == RpcProtocol.NETWORK_OBJECT_NEW || message.bytes[0] == RpcProtocol.NETWORK_OBJECT_DESTROY)
                )
                {
                    _spawner.Decode(message.bytes);
                }
                else
                {
                    var args = RpcAttribute.DecodeRpc(message.source, message.bytes, out var protocol, out var target);
                    // Console.WriteLine(protocol.Method.Name + " call received on " + GetNetworkObject(target)!.ToString() + ":");
                    // Console.WriteLine("  " + BitConverter.ToString(message.bytes));
                    // for (int i = 0; i < args.Length; i++)
                    //     if (args[i] != null) Console.WriteLine("   arg " + i + ": " + args[i]!.ToString());
                        protocol.Invoke(GetNetworkObject(target), args);
                    try
                    {
                    }
                    catch
                    {
                        Console.WriteLine("failed to execute RPC");
                    }
                }
            }
        }


        public void AwaitConnection()
        {
            if (!_buffer.IsReady)
                _buffer.Read();
        }

        public bool GetNextMessage(out NetworkBuffer.Message message)
        {
            return _buffer.GetNextMessage(out message);
        }

        /// <summary>
        /// Add message to outgoing buffers. Actually send buffers with <c>Send()</c>.
        /// </summary>
        public void Write(byte[] message)
        {
            _buffer.Write(message);
        }

        /// <summary>
        /// ONLY ALLOWED ON SERVER. Add message to the outgoing buffer of a specific client.
        /// Actually send buffers with <c>Send()</c>.
        /// </summary>
        public void WriteTo(ClientId id, byte[] message)
        {
            if (role == Role.Client)
                throw new InvalidOperationException("Clients cannot send messages directly to other clients.");
            _buffer.WriteTo(id, message);
        }

        /// <summary>
        /// Send current outgoing buffers.
        /// </summary>
        public void Send()
        {
            _buffer.Send();
        }

        /// <summary>
        /// Disconnect the local connection. If this is a server, the server is shut down.
        /// if this is a client, disconnect from the server.
        /// </summary>
        public void Disconnect()
        {
            _buffer.Disconnect();
            IsActive = false;
        }

        /// <summary>
        /// ONLY ALLOWED ON SERVER. Disconnect a specific client from the server.
        /// </summary>
        public void Disconnect(ClientId id)
        {
            if (role == Role.Client)
                throw new InvalidOperationException("Clients cannot disconnect other clients");
            _buffer.Disconnect(id);
        }

        private NetworkSpawner _spawner;

        /// <summary>
        /// Invoked when a new object is spawned. Provides the spawned object. 
        /// Invoked after the object's OnSpawn() method has been called.
        /// </summary>
        public event NetworkObject.Delegate? OnObjectSpawn;

        /// <summary>
        /// Invoked when an object is destroyed. Provides the "destroyed" object, marked as not active.
        /// Invoked after the object's OnDestroy() method has been called.
        /// </summary>
        public event NetworkObject.Delegate? OnObjectDestroy;

        /// <summary>
        /// Try to get an object with the given id. Returns true if one was found, false otherwise.
        /// </summary>
        public bool TryGetObject(NetworkId id, out NetworkObject? obj)
        {
            return _spawner.TryGetObject(id, out obj);
        }
        
        /// <summary>
        /// Get an object with the given id. Returns null if none exist.
        /// </summary>
        public NetworkObject? GetNetworkObject(NetworkId id)
        {
            return _spawner.GetNetworkObject(id);
        }

        /// <summary>
        /// Spawns a new instance of the given NetworkObject sub-type across all clients.
        /// </summary>
        public T Spawn<T>() where T : NetworkObject, new()
        {
            if (role == Role.Client)
                throw new InvalidOperationException("Clients cannot spawn or destroy network objects");
            return _spawner.Spawn<T>();
        }

        /// <summary>
        /// Spawns a new instance of the given NetworkObject sub-type across all clients.
        /// </summary>
        public object Spawn(Type t)
        {
            if (role == Role.Client)
                throw new InvalidOperationException("Clients cannot spawn or destroy network objects");
            return _spawner.Spawn(t);
        }

        /// <summary>
        /// Destroy the given NetworkObject across all clients.
        /// </summary>
        public void Destroy(NetworkObject target)
        {
            if (role == Role.Client)
                throw new InvalidOperationException("Clients cannot spawn or destroy network objects");
            _spawner.Destroy(target);
        }
    }
}