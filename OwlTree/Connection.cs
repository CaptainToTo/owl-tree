
using System;
using System.Collections.Concurrent;
using System.Threading;

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
        public class Args
        {
            // socket args

            /// <summary>
            /// Whether this connection is a server or client.<b>Default = Server</b>
            /// </summary>
            public Role role = Role.Server;
            /// <summary>
            /// The server IP address. <b>Default = localhost</b>
            /// </summary>
            public string serverAddr = "127.0.0.1";
            /// <summary>
            /// The server TCP port. <b>Default = 8000</b>
            /// </summary>
            public int tcpPort = 8000;
            /// <summary>
            /// The port the server will listen to for UDP packets. <b>Default = 9000</b>
            /// </summary>
            public int serverUdpPort = 9000;
            /// <summary>
            /// The port the client will listen to for UDP packets. <b>Default = 9010</b>
            /// </summary>
            public int clientUdpPort = 9010;
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

            // buffer transformers

            /// <summary>
            /// Add custom transformers that will be apply to data read from sockets. Steps will be sorted by priority, least to greatest,
            /// and executed in that order. <b>Default = None</b>
            /// </summary>
            public NetworkBuffer.Transformer[] readSteps = new NetworkBuffer.Transformer[0];

            /// <summary>
            /// Add custom transformers that will be apply to data sent to sockets. Steps will be sorted by priority, least to greatest,
            /// and executed in that order. <b>Default = None</b>
            /// </summary>
            public NetworkBuffer.Transformer[] sendSteps = new NetworkBuffer.Transformer[0];

            /// <summary>
            /// Adds Huffman encoding and decoding to the connection's read and send steps, with a priority of 100. <b>Default = true</b>
            /// </summary>
            public bool useCompression = true;

            // initial listeners


            // threaded buffer

            /// <summary>
            /// If false, Reading and writing to sockets will need to called by your program with <c>Read()</c>
            /// and <c>Send()</c>. These operations will be done synchronously.
            /// <br /><br />
            /// If true <b>(Default)</b>, reading and writing will be handled autonomously in a separate, dedicated thread. 
            /// Reading will fill a queue of RPCs to be executed in the main program thread by calling <c>ExecuteQueue()</c>.
            /// Reading and writing will be done at a regular frequency, as defined by the <c>threadUpdateDelta</c> arg.
            /// </summary>
            public bool threaded = true;

            /// <summary>
            /// If the connection is threaded, specify the number of milliseconds the read/write thread will spend sleeping
            /// between updates. <b>Default = 40 (25 ticks/sec)</b>
            /// </summary>
            public int threadUpdateDelta = 40;

            // logging

            /// <summary>
            /// Inject a function for outputting logs from the connection. <b>Default = Console.WriteLine</b>
            /// </summary>
            public Logger.Printer printer = Console.WriteLine;

            /// <summary>
            /// Select a verbosity for logs from the connection. This will determine how many logs will be output.
            /// <b>Default = LogRule.Events</b>
            /// </summary>
            public Logger.LogRule verbosity = Logger.LogRule.Events;

            public Args() { }
        }

        /// <summary>
        /// Create a new connection. Server instances will be immediately ready. 
        /// Clients will require an initial <c>StartConnection()</c> call.
        /// </summary>
        public Connection(Args args)
        {
            _logger = new Logger(args.printer, args.verbosity);

            RpcAttribute.GenerateRpcProtocols(_logger);

            if (args.role == Role.Client)
            {
                _buffer = new ClientBuffer(args.serverAddr, args.tcpPort, args.serverUdpPort, args.clientUdpPort, args.bufferSize, TryDecodeRpc, EncodeRpc);
            }
            else
            {
                _buffer = new ServerBuffer(args.serverAddr, args.tcpPort, args.serverUdpPort, args.clientUdpPort, args.maxClients, args.bufferSize, TryDecodeRpc, EncodeRpc);
                IsActive = true;
            }
            NetRole = args.role;
            _buffer.OnClientConnected = (id) => _clientEvents.Enqueue((ConnectionEventType.OnConnect, id));
            _buffer.OnClientDisconnected = (id) => _clientEvents.Enqueue((ConnectionEventType.OnDisconnect, id));
            _buffer.OnReady = (id) => _clientEvents.Enqueue((ConnectionEventType.OnReady, id));

            _spawner = new NetworkSpawner(this);

            _spawner.OnObjectSpawn = (obj) => {
                _logger.Write(Logger.LogRule.Events, "Spawned new network object: " + obj.Id.ToString() + ", of type: " + obj.GetType().Name);
                OnObjectSpawn?.Invoke(obj);
            };
            _spawner.OnObjectDespawn = (obj) => {
                _logger.Write(Logger.LogRule.Events, "Despawned network object: " + obj.Id.ToString() + ", of type: " + obj.GetType().Name);
                OnObjectDespawn?.Invoke(obj);
            };

            if (args.useCompression)
            {
                _buffer.AddReadStep(new NetworkBuffer.Transformer{
                    priority = 100,
                    step = Huffman.Decode
                });

                _buffer.AddSendStep(new NetworkBuffer.Transformer{
                    priority = 100,
                    step = Huffman.Encode
                });
            }

            foreach (var step in args.readSteps)
            {
                _buffer.AddReadStep(step);
            }
            foreach (var step in args.sendSteps)
            {
                _buffer.AddSendStep(step);
            }

            if (args.threaded)
            {
                Threaded = false;
                _threadUpdateDelta = args.threadUpdateDelta;
                _bufferThread = new Thread(new ThreadStart(NetworkLoop));
                _bufferThread.Start();
            }
        }

        private Logger _logger;

        private Thread _bufferThread = null;

        public bool Threaded { get; private set; } = false;
        private int _threadUpdateDelta = 40;

        private void NetworkLoop()
        {
            AwaitConnection();
            while (true)
            {
                long start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _buffer.Read();
                if (_buffer.HasOutgoing)
                    _buffer.Send();
                long diff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - start;

                Thread.Sleep((int)(_threadUpdateDelta - diff));
            }
        }

        /// <summary>
        /// Whether this connection represents a server or client.
        /// </summary>
        public Role NetRole { get; private set; }

        private NetworkBuffer _buffer;

        private enum ConnectionEventType
        {
            OnConnect,
            OnDisconnect,
            OnReady
        }

        private ConcurrentQueue<(ConnectionEventType t, ClientId id)> _clientEvents = new ConcurrentQueue<(ConnectionEventType, ClientId)>();

        private ConcurrentQueue<ClientId> _disconnectRequests = new ConcurrentQueue<ClientId>();

        /// <summary>
        /// Invoked when a new client connects. Provides the id of the new client.
        /// </summary>
        public event ClientId.Delegate OnClientConnected;

        /// <summary>
        /// Invoked when a client disconnects. Provides the id of the disconnected client.
        /// </summary>
        public event ClientId.Delegate OnClientDisconnected;

        /// <summary>
        /// Invoked when the local connection is ready. On a server, provides <c>ClientId.None</c>.
        /// On a client, provides the local client id, as assigned by the server.
        /// </summary>
        public event ClientId.Delegate OnReady;

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
            if (Threaded)
                throw new InvalidOperationException("Cannot perform read operation on a threaded connection. This is handled for you in a dedicated thread.");
            if (IsActive)
                _buffer.Read();
        }

        public void AwaitConnection()
        {
            if (Threaded)
                throw new InvalidOperationException("Cannot perform await connection operation on a threaded connection. This is handled for you in a dedicated thread.");
            if (!_buffer.IsReady)
                _buffer.Read();
        }

        internal bool GetNextMessage(out NetworkBuffer.Message message)
        {
            return _buffer.GetNextMessage(out message);
        }

        public void ExecuteQueue()
        {
            while (_clientEvents.TryDequeue(out var result))
            {
                switch (result.t)
                {
                    case ConnectionEventType.OnConnect:
                        _logger.Write(Logger.LogRule.Events, "New client connected: " + result.id.ToString());
                        OnClientConnected?.Invoke(result.id);

                        if (NetRole == Role.Server)
                        {
                            _spawner.SendNetworkObjects(result.id);
                        }
                        break;
                    case ConnectionEventType.OnDisconnect:
                        if (result.id == LocalId)
                        {
                            _logger.Write(Logger.LogRule.Events, "Local client disconnected.");
                            IsActive = false;
                            _spawner.DespawnAll();
                        }
                        else
                        {
                            _logger.Write(Logger.LogRule.Events, "Remote client disconnected: " + result.id.ToString());
                        }
                        OnClientDisconnected?.Invoke(result.id);
                        break;
                    case ConnectionEventType.OnReady:
                        IsActive = true;
                        _logger.Write(Logger.LogRule.Events, "Connection is ready. Local client id is: " + result.id.ToString());
                        OnReady?.Invoke(result.id);
                        break;
                }
            }

            if (NetRole == Role.Server)
            {
                while (_disconnectRequests.TryDequeue(out var clientId))
                {
                    _buffer.Disconnect(clientId);
                }
            }

            if (!IsActive)
                return;

            while (GetNextMessage(out var message))
            {
                if (
                    NetRole == Role.Client && 
                    (message.rpcId == RpcId.NETWORK_OBJECT_SPAWN || message.rpcId == RpcId.NETWORK_OBJECT_DESPAWN)
                )
                {
                    _spawner.ReceiveInstruction(message.rpcId, message.args);
                }
                else if (TryGetObject(message.target, out var target))
                {
                    RpcAttribute.InvokeRpc(message.rpcId, target, message.args);
                }
            }
        }

        private bool TryDecodeRpc(ClientId caller, ReadOnlySpan<byte> bytes, out NetworkBuffer.Message message)
        {
            message = NetworkBuffer.Message.Empty;
            if (NetworkSpawner.TryDecode(bytes, out var rpcId, out var args) && NetRole == Role.Client)
            {
                message = new NetworkBuffer.Message(LocalId, rpcId, args);
                return true;
            }
            else if (RpcAttribute.TryDecodeRpc(caller, bytes, out rpcId, out var target, out args))
            {
                message = new NetworkBuffer.Message(caller, LocalId, rpcId, target, Protocol.Tcp, args);
                return true;
            }
            return false;
        }

        private void EncodeRpc(NetworkBuffer.Message message, MessageBuffer buffer)
        {
            var span = buffer.GetSpan(message.bytes.Length);
            for (int i = 0; i < span.Length; i++)
            {
                span[i] = message.bytes[i];
            }
        }

        internal void AddRpc(ClientId callee, RpcId rpcId, NetworkId target, Protocol protocol, object[] args)
        {
            var message = new NetworkBuffer.Message(LocalId, callee, rpcId, target, protocol, args);

            if (message.rpcId == RpcId.NETWORK_OBJECT_SPAWN)
            {
                message.bytes = new byte[NetworkSpawner.SpawnByteLength];
                NetworkSpawner.SpawnEncode(message.bytes, (Type)message.args[0], (NetworkId)message.args[1]);
            }
            else if (message.rpcId == RpcId.NETWORK_OBJECT_DESPAWN)
            {
                message.bytes = new byte[NetworkSpawner.DespawnByteLength];
                NetworkSpawner.DespawnEncode(message.bytes, (NetworkId)message.args[0]);
            }
            else
            {
                message.bytes = new byte[RpcAttribute.RpcExpectedLength(message.rpcId, message.args)];
                RpcAttribute.EncodeRpc(message.bytes, message.rpcId, message.target, message.args);
                if (_logger.IncludesVerbose)
                    _logger.Write(Logger.LogRule.Verbose, RpcAttribute.GetEncodingSummary(rpcId, target, args, LocalId));
            }

            _buffer.AddMessage(message);
        }

        internal void AddRpc(ClientId callee, RpcId rpcId, Protocol protocol, object[] args)
        {
            AddRpc(callee, rpcId, NetworkId.None, protocol, args);
        }

        internal void AddRpc(RpcId rpcId, object[] args)
        {
            AddRpc(ClientId.None, rpcId, NetworkId.None, Protocol.Tcp, args);
        }

        /// <summary>
        /// Send current outgoing buffers.
        /// </summary>
        public void Send()
        {
            if (Threaded)
                throw new InvalidOperationException("Cannot perform send operation on a threaded connection. This is handled for you in a dedicated thread.");
            if (IsActive)
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
            _spawner.DespawnAll();
        }

        /// <summary>
        /// ONLY ALLOWED ON SERVER. Disconnect a specific client from the server.
        /// </summary>
        public void Disconnect(ClientId id)
        {
            if (NetRole == Role.Client)
                throw new InvalidOperationException("Clients cannot disconnect other clients");
            if (Threaded)
                _disconnectRequests.Enqueue(id);
            else
                _buffer.Disconnect(id);
        }

        private NetworkSpawner _spawner;

        /// <summary>
        /// Invoked when a new object is spawned. Provides the spawned object. 
        /// Invoked after the object's OnSpawn() method has been called.
        /// </summary>
        public event NetworkObject.Delegate OnObjectSpawn;

        /// <summary>
        /// Invoked when an object is despawned. Provides the "despawned" object, marked as not active.
        /// Invoked after the object's OnDespawn() method has been called.
        /// </summary>
        public event NetworkObject.Delegate OnObjectDespawn;

        /// <summary>
        /// Try to get an object with the given id. Returns true if one was found, false otherwise.
        /// </summary>
        public bool TryGetObject(NetworkId id, out NetworkObject obj)
        {
            return _spawner.TryGetObject(id, out obj);
        }
        
        /// <summary>
        /// Get an object with the given id. Returns null if none exist.
        /// </summary>
        public NetworkObject GetNetworkObject(NetworkId id)
        {
            return _spawner.GetNetworkObject(id);
        }

        /// <summary>
        /// Spawns a new instance of the given NetworkObject sub-type across all clients.
        /// </summary>
        public T Spawn<T>() where T : NetworkObject, new()
        {
            if (NetRole == Role.Client)
                throw new InvalidOperationException("Clients cannot spawn or destroy network objects");
            var obj = _spawner.Spawn<T>();
            return obj;
        }

        /// <summary>
        /// Spawns a new instance of the given NetworkObject sub-type across all clients.
        /// </summary>
        public object Spawn(Type t)
        {
            if (NetRole == Role.Client)
                throw new InvalidOperationException("Clients cannot spawn or despawn network objects");
            return _spawner.Spawn(t);
        }

        /// <summary>
        /// Despawns the given NetworkObject across all clients.
        /// </summary>
        public void Despawn(NetworkObject target)
        {
            if (NetRole == Role.Client)
                throw new InvalidOperationException("Clients cannot spawn or despawn network objects");
            _spawner.Despawn(target);
        }
    }
}