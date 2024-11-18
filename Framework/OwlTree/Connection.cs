
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
            /// The number of milliseconds clients will wait before sending another connection request to the server.
            /// <b>Default = 5000 (5 sec)</b>
            /// </summary>
            public int connectionRequestRate = 5000;

            /// <summary>
            /// The number of connection attempts clients will make before ending the connection in failure. <b>Default = 10</b>
            /// </summary>
            public int connectionRequestLimit = 10;

            /// <summary>
            /// The byte length of read and write buffers.
            /// <b>Default = 2048</b>
            /// </summary>
            public int bufferSize = 2048;

            // app data

            /// <summary>
            /// The version of Owl Tree this connection is running on. 
            /// This value can be lowered from the default to use older formats of Owl Tree. 
            /// <b>Default = Current Version</b>
            /// </summary>
            public ushort owlTreeVersion = 1;

            /// <summary>
            /// The minimum Owl Tree version that will be supported. If clients using an older version attempt to connect,
            /// they will be rejected. <b>Default = 0 (always accept)</b>
            /// </summary>
            public ushort minOwlTreeVersion = 0;

            /// <summary>
            /// The version of your app this connection is running on. <b>Default = 1</b>
            /// </summary>
            public ushort appVersion = 1;

            /// <summary>
            /// The minimum app version that will be supported. If clients using an older version attempt to connect,
            /// they will be rejected. <b>Default = 0 (always accept)</b>
            /// </summary>
            public ushort minAppVersion = 0;

            /// <summary>
            /// A unique, max 64 ASCII character id used for simple client verification. <b>Default = "MyOwlTreeApp"</b>
            /// </summary>
            public string appId = "MyOwlTreeApp";

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
            /// Specify what information will get logged. <b>Default = None</b>
            /// </summary>
            public Logger.IncludeRules verbosity = Logger.Includes();

            public Args() { }
        }

        /// <summary>
        /// Create a new connection. Server instances will be immediately ready. 
        /// Clients will require an initial <c>StartConnection()</c> call.
        /// </summary>
        public Connection(Args args)
        {
            _logger = new Logger(args.printer, args.verbosity);

            Protocols = RpcProtocols.GetProjectImplementation();

            if (_logger.includes.allRpcProtocols)
            {
                _logger.Write(Protocols.GetAllProtocolSummaries());
            }

            NetworkBuffer.Args bufferArgs = new NetworkBuffer.Args(){
                owlTreeVer = args.owlTreeVersion,
                minOwlTreeVer = args.minOwlTreeVersion,
                appVer = args.appVersion,
                minAppVer = args.minAppVersion,
                appId = args.appId,
                addr = args.serverAddr,
                tcpPort = args.tcpPort,
                serverUdpPort = args.serverUdpPort,
                clientUdpPort = args.clientUdpPort,
                bufferSize = args.bufferSize,
                encoder = EncodeRpc,
                decoder = TryDecodeRpc,
                logger = _logger
            };

            if (args.role == Role.Client)
            {
                _buffer = new ClientBuffer(bufferArgs, args.connectionRequestRate, args.connectionRequestLimit);
                IsReady = false;
            }
            else
            {
                _buffer = new ServerBuffer(bufferArgs, args.maxClients);
                IsReady = true;
            }
            NetRole = args.role;
            _buffer.OnClientConnected = (id) => _clientEvents.Enqueue((ConnectionEventType.OnConnect, id));
            _buffer.OnClientDisconnected = (id) => _clientEvents.Enqueue((ConnectionEventType.OnDisconnect, id));
            _buffer.OnReady = (id) => _clientEvents.Enqueue((ConnectionEventType.OnReady, id));
            IsActive = true;

            var factory = ProxyFactory.GetProjectImplementation();

            if (_logger.includes.allTypeIds)
                _logger.Write(factory.GetAllIdAssignments());

            _spawner = new NetworkSpawner(this, factory);

            _spawner.OnObjectSpawn = (obj) => {
                if (_logger.includes.spawnEvents)
                    _logger.Write("Spawned new network object: " + obj.Id.ToString() + ", of type: " + obj.GetType().ToString());
                OnObjectSpawn?.Invoke(obj);
            };
            _spawner.OnObjectDespawn = (obj) => {
                if (_logger.includes.spawnEvents)
                    _logger.Write("Despawned network object: " + obj.Id.ToString() + ", of type: " + obj.GetType().ToString());
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
        
        /// <summary>
        /// Access metadata about RPC encodings and generated protocols.
        /// </summary>
        public RpcProtocols Protocols { get; private set; }

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

                Thread.Sleep(Math.Max(0, (int)(_threadUpdateDelta - diff)));
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

        private List<ClientId> _clients = new List<ClientId>();

        /// <summary>
        /// Iterable of all connected clients.
        /// </summary>
        public IEnumerable<ClientId> Clients { get { return _clients; } }

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
        /// Whether this connection has established a link to the server. This is true for clients once they've been assigned a local id.
        /// </summary>
        public bool IsReady { get; private set; } = false;

        /// <summary>
        /// The client id assigned to this local instance. Servers will have a LocalId of <c>ClientId.None</c>
        /// </summary>
        public ClientId LocalId { get { return _buffer.LocalId; } }

        /// <summary>
        /// Receive any RPCs that have been sent to this connection. Execute them with <c>ExecuteQueue()</c>.
        /// </summary>
        public void Read()
        {
            if (Threaded)
                throw new InvalidOperationException("Cannot perform read operation on a threaded connection. This is handled for you in a dedicated thread.");
            if (IsActive)
                _buffer.Read();
        }

        /// <summary>
        /// Block until the connection is ready.
        /// </summary>
        public void AwaitConnection()
        {
            if (Threaded)
                throw new InvalidOperationException("Cannot perform await connection operation on a threaded connection. This is handled for you in a dedicated thread.");
            while (!_buffer.IsReady)
            {
                _buffer.Read();
                Thread.Sleep(_threadUpdateDelta);
            }
        }

        internal bool GetNextMessage(out NetworkBuffer.Message message)
        {
            return _buffer.GetNextMessage(out message);
        }

        /// <summary>
        /// Execute any RPCs that have been received in the last <c>Read()</c>.
        /// </summary>
        public void ExecuteQueue()
        {
            while (_clientEvents.TryDequeue(out var result))
            {
                switch (result.t)
                {
                    case ConnectionEventType.OnConnect:
                        if (_logger.includes.clientEvents)
                            _logger.Write("New client connected: " + result.id.ToString());
                        if (NetRole == Role.Server)
                        {
                            _spawner.SendNetworkObjects(result.id);
                        }

                        _clients.Add(result.id);
                        OnClientConnected?.Invoke(result.id);
                        break;
                    case ConnectionEventType.OnDisconnect:
                        if (result.id == LocalId)
                        {
                            if (_logger.includes.clientEvents)
                                _logger.Write("Local client disconnected.");
                            IsActive = false;
                            IsReady = false;
                            _spawner.DespawnAll();
                        }
                        else
                        {
                            if (_logger.includes.clientEvents)
                                _logger.Write("Remote client disconnected: " + result.id.ToString());
                        }
                        _clients.Remove(result.id);
                        OnClientDisconnected?.Invoke(result.id);
                        break;
                    case ConnectionEventType.OnReady:
                        IsReady = true;
                        if (_logger.includes.clientEvents)
                            _logger.Write("Connection is ready. Local client id is: " + result.id.ToString());
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
                    Protocols.InvokeRpc(message.caller, message.callee, message.rpcId, target, message.args);
                }
            }
        }

        private bool TryDecodeRpc(ClientId caller, ReadOnlySpan<byte> bytes, out NetworkBuffer.Message message)
        {
            message = NetworkBuffer.Message.Empty;
            if (NetworkSpawner.TryDecode(bytes, out var rpcId, out var args) && NetRole == Role.Client)
            {
                message = new NetworkBuffer.Message(LocalId, rpcId, args);
                if (_logger.includes.rpcReceiveEncodings)
                {
                    if (rpcId.Id == RpcId.NETWORK_OBJECT_SPAWN)
                        _logger.Write("RECEIVING:\n" + _spawner.SpawnEncodingSummary((byte)message.args[0], (NetworkId)message.args[1]));
                    else
                        _logger.Write("RECEIVING:\n" + _spawner.DespawnEncodingSummary((NetworkId)args[0]));
                }
                return true;
            }
            else if (Protocols.TryDecodeRpc(caller, bytes, out rpcId, out var target, out args))
            {
                message = new NetworkBuffer.Message(caller, LocalId, rpcId, target, Protocol.Tcp, args);
                if (_logger.includes.rpcReceives)
                {
                    var output = $"RECEIVING: {Protocols.GetRpcName(rpcId.Id)} {rpcId}, Called on Object {target}";
                    if (_logger.includes.rpcReceiveEncodings)
                        output += ":\n" + Protocols.GetEncodingSummary(LocalId, rpcId, target, args);
                    _logger.Write(output);
                }
                return true;
            }
            return false;
        }

        private void EncodeRpc(NetworkBuffer.Message message, Packet buffer)
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
                _spawner.SpawnEncode(message.bytes, (Type)message.args[0], (NetworkId)message.args[1]);
                if (_logger.includes.rpcCallEncodings)
                    _logger.Write("SENDING:\n" + _spawner.SpawnEncodingSummary((Type)message.args[0], (NetworkId)message.args[1]));
            }
            else if (message.rpcId == RpcId.NETWORK_OBJECT_DESPAWN)
            {
                message.bytes = new byte[NetworkSpawner.DespawnByteLength];
                _spawner.DespawnEncode(message.bytes, (NetworkId)message.args[0]);
                if (_logger.includes.rpcCallEncodings)
                    _logger.Write("SENDING:\n" + _spawner.DespawnEncodingSummary((NetworkId)message.args[0]));
            }
            else
            {
                message.bytes = new byte[RpcEncoding.GetExpectedRpcLength(message.args)];
                RpcEncoding.EncodeRpc(message.bytes, message.rpcId, message.target, message.args);
                if (_logger.includes.rpcCalls)
                {
                    var output = $"SENDING: {Protocols.GetRpcName(rpcId.Id)} {rpcId}, Called on Object {target}";
                    if (_logger.includes.rpcCallEncodings)
                        output += ":\n" + Protocols.GetEncodingSummary(LocalId, rpcId, target, args);
                    _logger.Write(output);
                }
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
            IsReady = false;
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