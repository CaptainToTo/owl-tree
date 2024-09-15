
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
        public struct Args
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

            /// <summary>
            /// If false <b>(Default)</b>, Reading and writing to sockets will need to called by your program with <c>Read()</c>
            /// and <c>Send()</c>. These operations will be done synchronously.
            /// <br /><br />
            /// If true, reading and writing will be handled autonomously in a separate, dedicated thread. 
            /// Reading will fill a queue of RPCs to be executed in the main program thread by calling <c>ExecuteQueue()</c>.
            /// Reading and writing will be done at a regular frequency, as defined by the <c>threadUpdateDelta</c> arg.
            /// </summary>
            public bool threaded = false;

            /// <summary>
            /// If the connection is threaded, specify the number of milliseconds the read/write thread will spend sleeping
            /// between updates. <b>Default = 17 (60 ticks/sec)</b>
            /// </summary>
            public int threadUpdateDelta = 17;

            public Args() { }
        }

        /// <summary>
        /// Create a new connection. Server instances will be immediately ready. 
        /// Clients will require an initial <c>StartConnection()</c> call.
        /// </summary>
        public Connection(Args args)
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

            _spawner = new NetworkSpawner(this);

            _spawner.OnObjectSpawn = (obj) => OnObjectSpawn?.Invoke(obj);
            _spawner.OnObjectDespawn = (obj) => OnObjectDespawn?.Invoke(obj);
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
        }

        public void AwaitConnection()
        {
            if (!_buffer.IsReady)
                _buffer.Read();
        }

        internal bool GetNextMessage(out NetworkBuffer.Message message)
        {
            return _buffer.GetNextMessage(out message);
        }

        public void ExecuteQueue()
        {
            while (GetNextMessage(out var message))
            {
                if (
                    role == Role.Client && 
                    (message.rpcId == RpcProtocol.NETWORK_OBJECT_NEW || message.rpcId == RpcProtocol.NETWORK_OBJECT_DESTROY)
                )
                {
                    _spawner.Decode(message.rpcId, message.args);
                }
                else if (TryGetObject(message.target, out var target))
                {
                    RpcAttribute.InvokeRpc(message.rpcId, target!, message.args);
                }
            }
        }

        internal void AddRpc(ClientId callee, byte rpcId, NetworkId target, object[]? args)
        {
            _buffer.AddMessage(new NetworkBuffer.Message(LocalId, callee, rpcId, target, args));
        }

        internal void AddRpc(ClientId callee, byte rpcId, object[]? args)
        {
            _buffer.AddMessage(new NetworkBuffer.Message(LocalId, callee, rpcId, NetworkId.None, args));
        }

        internal void AddRpc(byte rpcId, object[]? args)
        {
            _buffer.AddMessage(new NetworkBuffer.Message(LocalId, ClientId.None, rpcId, NetworkId.None, args));
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
        /// Invoked when an object is despawned. Provides the "despawned" object, marked as not active.
        /// Invoked after the object's OnDespawn() method has been called.
        /// </summary>
        public event NetworkObject.Delegate? OnObjectDespawn;

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
            var obj = _spawner.Spawn<T>();
            obj.OnRpcCall = AddRpc;
            return obj;
        }

        /// <summary>
        /// Spawns a new instance of the given NetworkObject sub-type across all clients.
        /// </summary>
        public object Spawn(Type t)
        {
            if (role == Role.Client)
                throw new InvalidOperationException("Clients cannot spawn or despawn network objects");
            return _spawner.Spawn(t);
        }

        /// <summary>
        /// Despawns the given NetworkObject across all clients.
        /// </summary>
        public void Despawn(NetworkObject target)
        {
            if (role == Role.Client)
                throw new InvalidOperationException("Clients cannot spawn or despawn network objects");
            _spawner.Despawn(target);
        }
    }
}