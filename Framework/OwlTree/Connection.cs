
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

namespace OwlTree
{
    /// <summary>
    /// Primary interface for OwlTree server-client connections. 
    /// </summary>
    public class Connection
    {
        /// <summary>
        /// Determines the responsibilities and capabilities a Connection will have.
        /// </summary>
        public enum Role
        {
            /// <summary>
            /// The Connection is a Server, it will manage client connections, and act as the state authority.
            /// </summary>
            Server,
            /// <summary>
            /// The Connection is a Client, it will attempt to connect to a server, and will not have state authority.
            /// </summary>
            Client,
            /// <summary>
            /// The Connection is a Host Client, it will attempt to connect to a server, and will act as the state authority.
            /// </summary>
            Host,
            /// <summary>
            /// The Connection is Relay Server, it will manage client connections, and pass RPCs between host and clients.
            /// </summary>
            Relay
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
            /// The maximum number of clients the server will allow to be connected at once.
            /// <b>Default = 4</b>
            /// </summary>
            public int maxClients = 4;
            /// <summary>
            /// The IP address of the host client of this session. Used by relay server 
            /// to pre-verify the host. <b>Default = null (first client connected will be given host authority)</b>
            /// </summary>
            public string hostAddr = null;
            /// <summary>
            /// Whether or not a relayed peer-to-peer session can migrate hosts. 
            /// A session that is migratable will re-assign the host if the current host disconnects.
            /// A session that is not migratable will shutdown if the current host disconnects.
            /// <b>Default = false</b>
            /// </summary>
            public bool migratable = false;
            /// <summary>
            /// Provide a server a list of IP addresses that will be the only IPs allowed to connect as clients.
            /// If left as null, then any IP address will be allowed to connect.
            /// <b>Default = null</b>
            /// </summary>
            public IPAddress[] whitelist = null;

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
            /// the number of milliseconds servers will wait for clients to make the TCP handshake before timing out
            /// their connection request. <b>Default = 20000 (20 sec)</b>
            /// </summary>
            public int connectionRequestTimeout = 20000;

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
            NetRole = args.role;

            _logger = new Logger(args.printer, args.verbosity);

            Protocols = IsRelay ? null : RpcProtocols.GetProjectImplementation();

            if (Protocols == null)
                    _logger.Write("WARNING: No project RPC protocols found. Ensure the OwlTree source generator is included in your project properly.");

            if (Protocols != null && _logger.includes.allRpcProtocols)
                _logger.Write(Protocols.GetAllProtocolSummaries());

            NetworkBuffer.Args bufferArgs = new NetworkBuffer.Args(){
                owlTreeVer = args.owlTreeVersion,
                minOwlTreeVer = args.minOwlTreeVersion,
                appVer = args.appVersion,
                minAppVer = args.minAppVersion,
                appId = args.appId,
                addr = args.serverAddr,
                tcpPort = args.tcpPort,
                serverUdpPort = args.serverUdpPort,
                bufferSize = args.bufferSize,
                encoder = EncodeRpc,
                decoder = TryDecodeRpc,
                logger = _logger
            };

            switch (args.role)
            {
                case Role.Server:
                    _buffer = new ServerBuffer(bufferArgs, args.maxClients, args.connectionRequestTimeout, args.whitelist);
                    IsReady = true;
                    break;
                case Role.Relay:
                    _buffer = new RelayBuffer(bufferArgs, args.maxClients, args.connectionRequestTimeout, args.hostAddr, args.migratable, args.whitelist);
                    IsReady = true;
                    break;
                case Role.Client:
                case Role.Host:
                    _buffer = new ClientBuffer(bufferArgs, args.connectionRequestRate, args.connectionRequestLimit, IsHost);
                    IsReady = false;
                    break;
            }
            _buffer.OnClientConnected = (id) => _clientEvents.Enqueue((ConnectionEventType.OnConnect, id));
            _buffer.OnClientDisconnected = (id) => _clientEvents.Enqueue((ConnectionEventType.OnDisconnect, id));
            _buffer.OnReady = (id) => _clientEvents.Enqueue((ConnectionEventType.OnReady, id));
            _buffer.OnHostMigration = (id) => _clientEvents.Enqueue((ConnectionEventType.OnHostMigration, id));
            IsActive = true;

            if (!IsRelay)
            {
                var factory = ProxyFactory.GetProjectImplementation();

                if (factory == null)
                    _logger.Write("WARNING: No project network object factory found. Ensure the OwlTree source generator is included in your project properly.");

                if (factory != null && _logger.includes.allTypeIds)
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
            }

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
                Threaded = true;
                _threadUpdateDelta = args.threadUpdateDelta;
                _clientRequests = new();
                _bufferThread = new Thread(NetworkLoop);
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
            while (!_buffer.IsReady)
            {
                _buffer.Read();
                Thread.Sleep(_threadUpdateDelta);
            }
            while (_buffer.IsActive)
            {
                long start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _buffer.Read();

                if (!IsClient)
                {
                    while (_clientRequests.TryDequeue(out var request))
                    {
                        switch (request.t)
                        {
                            case ConnectionEventType.OnDisconnect:
                                _buffer.Disconnect(request.id);
                                break;
                            case ConnectionEventType.OnHostMigration:
                                _buffer.MigrateHost(request.id);
                                break;
                        }
                    }
                }

                if (_buffer.HasOutgoing)
                    _buffer.Send();
                long diff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - start;

                Thread.Sleep(Math.Max(0, _threadUpdateDelta - (int)diff));
            }
        }

        /// <summary>
        /// Whether this connection represents a server or client.
        /// </summary>
        public Role NetRole { get; private set; }

        /// <summary>
        /// Returns true if this connection is configured to be a server.
        /// </summary>
        public bool IsServer { get => NetRole == Role.Server; }

        /// <summary>
        /// Returns true if this connection is configured to be a client.
        /// </summary>
        public bool IsClient { get => NetRole == Role.Client; }

        /// <summary>
        /// Returns true if this connection is configured to be a host client.
        /// </summary>
        public bool IsHost { get => NetRole == Role.Host; }

        /// <summary>
        /// Returns true if this connection is configured to be a relay server.
        /// </summary>
        public bool IsRelay { get => NetRole == Role.Relay; }

        private NetworkBuffer _buffer;

        private enum ConnectionEventType
        {
            OnConnect,
            OnDisconnect,
            OnReady,
            OnHostMigration
        }

        private ConcurrentQueue<(ConnectionEventType t, ClientId id)> _clientEvents = new();

        private ConcurrentQueue<(ConnectionEventType t, ClientId id)> _clientRequests = null;

        private List<ClientId> _clients = new List<ClientId>();

        /// <summary>
        /// The number of connected clients.
        /// </summary>
        public int ClientCount => _clients.Count;

        /// <summary>
        /// Iterable of all connected clients.
        /// </summary>
        public IEnumerable<ClientId> Clients { get { return _clients; } }

        /// <summary>
        /// Returns true if the given client id currently exists on this connection.
        /// </summary>
        public bool ContainsClient(ClientId id) => _clients.Contains(id);

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
        /// Invoke when this connection is closed. Provides the local client id.
        /// </summary>
        public event ClientId.Delegate OnLocalDisconnect;

        /// <summary>
        /// Invoked when the authority is migrated. Provides the new authority's client id.
        /// </summary>
        public event ClientId.Delegate OnHostMigration;

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
        public ClientId LocalId { get { return IsReady ? _buffer.LocalId : ClientId.None; } }

        /// <summary>
        /// the client id of the instance assigned as the authority of the session. 
        /// Servers will have an id of <c>ClientId.None</c>.
        /// </summary>
        public ClientId Authority { get; private set; } = ClientId.None;

        /// <summary>
        /// Returns true if the local connection is the authority of this session.
        /// </summary>
        public bool IsAuthority { get { return !IsRelay && _buffer.LocalId == _buffer.Authority; } }

        /// <summary>
        /// Returns true if the current session supports host migration.
        /// </summary>
        public bool Migratable { get { return IsRelay ? ((RelayBuffer)_buffer).Migratable : false; } }

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
            if (!IsActive)
                return;

            while (_clientEvents.TryDequeue(out var result))
            {
                switch (result.t)
                {
                    case ConnectionEventType.OnConnect:
                        if (_logger.includes.clientEvents)
                            _logger.Write("New client connected: " + result.id.ToString());
                        if (IsServer)
                        {
                            _spawner.SendNetworkObjects(result.id);
                        }
                        else if (IsRelay && result.id == _buffer.Authority)
                        {
                            Authority = result.id;
                            if (_logger.includes.clientEvents)
                                _logger.Write("Host client has been assigned to: " + result.id.ToString());
                        }

                        _clients.Add(result.id);
                        OnClientConnected?.Invoke(result.id);
                        break;
                    case ConnectionEventType.OnDisconnect:
                        if (result.id == LocalId)
                        {
                            if (_logger.includes.clientEvents)
                                _logger.Write(IsServer || IsRelay ? "Local server shutdown." : "Local client disconnected.");
                            IsActive = false;
                            IsReady = false;
                            OnLocalDisconnect?.Invoke(LocalId);
                            _spawner?.DespawnAll();
                        }
                        else
                        {
                            if (_logger.includes.clientEvents)
                                _logger.Write("Remote client disconnected: " + result.id.ToString());
                            _clients.Remove(result.id);
                            OnClientDisconnected?.Invoke(result.id);
                        }
                        break;
                    case ConnectionEventType.OnReady:
                        IsReady = true;
                        Authority = _buffer.Authority;
                        if (_logger.includes.clientEvents)
                            _logger.Write($"Connection is ready. Local client id is: {LocalId}, authority id is: {Authority}");
                        if (LocalId == Authority && IsClient)
                        {
                            NetRole = Role.Host;
                            if (_logger.includes.clientEvents)
                                _logger.Write("Local client assigned as host, this connection now has authority privileges.");
                        }
                        else if (LocalId != Authority && IsHost)
                        {
                            NetRole = Role.Client;
                            if (_logger.includes.clientEvents)
                                _logger.Write("Local connection requested to be host, but has been downgraded to client. Authority privileges removed.");
                        }
                        _clients.Add(result.id);
                        OnReady?.Invoke(result.id);
                        break;
                    case ConnectionEventType.OnHostMigration:
                        Authority = result.id;
                        if (NetRole == Role.Host)
                            NetRole = Role.Client;
                        if (result.id == LocalId)
                            NetRole = Role.Host;
                        if (_logger.includes.clientEvents)
                            _logger.Write("Host migrated, new authority is: " + result.id.ToString());
                        OnHostMigration?.Invoke(result.id);
                        break;
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
                    try
                    {
                        _spawner.ReceiveInstruction(message.rpcId, message.args);
                    }
                    catch (Exception e)
                    {
                        if (_logger.includes.exceptions)
                            _logger.Write($"Failed to run {(message.rpcId == RpcId.NETWORK_OBJECT_SPAWN ? "spawn" : "despawn")} instruction. Exception thrown:\n   {e}");
                    }
                }
                else if (message.rpcId == RpcId.PING_REQUEST)
                {
                    var request = (PingRequest)message.args[0];
                    request.PingResolved();
                }
                else if (TryGetObject(message.target, out var target))
                {
                    try
                    {
                        Protocols.InvokeRpc(message.caller, message.callee, message.rpcId, target, message.args);
                    }
                    catch (Exception e)
                    {
                        if (_logger.includes.exceptions)
                            _logger.Write($"Failed to run RPC {(Protocols?.GetRpcName(message.rpcId) ?? "Unknown")} {message.rpcId} on network object: {message.target}. Exception thrown:\n   {e}");
                    }
                }
            }
        }

        private bool TryDecodeRpc(ClientId source, ReadOnlySpan<byte> bytes, out NetworkBuffer.Message message)
        {
            message = NetworkBuffer.Message.Empty;
            if (NetRole != Role.Server && NetworkSpawner.TryDecode(bytes, out var rpcId, out var args))
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
            else if (Protocols != null && Protocols.TryDecodeRpc(bytes, out rpcId, out var caller, out var callee, out var target, out args))
            {
                message = new NetworkBuffer.Message(caller, callee, rpcId, target, Protocol.Tcp, args);
                if (_logger.includes.rpcReceives)
                {
                    var output = $"RECEIVING:\n{Protocols.GetRpcName(rpcId.Id)} {rpcId}, Called on Object {target}";
                    if (_logger.includes.rpcReceiveEncodings)
                        output += ":\n" + Protocols.GetEncodingSummary(rpcId, caller, callee, target, args);
                    _logger.Write(output);
                }
                return true;
            }
            return false;
        }

        private void EncodeRpc(NetworkBuffer.Message message, Packet buffer)
        {
            // add locally called rpc to packet
            if (message.bytes != null)
            {
                var span = buffer.GetSpan(message.bytes.Length);
                for (int i = 0; i < span.Length; i++)
                {
                    span[i] = message.bytes[i];
                }
            }
            // relaying client to client rpc
            else
            {
                if (message.rpcId == RpcId.NETWORK_OBJECT_SPAWN)
                {
                    try
                    {
                        var bytes = buffer.GetSpan(NetworkSpawner.SpawnByteLength);
                        _spawner.SpawnEncode(bytes, (Type)message.args[0], (NetworkId)message.args[1]);
                        if (_logger.includes.rpcCallEncodings)
                            _logger.Write("RELAYING:\n" + _spawner.SpawnEncodingSummary((Type)message.args[0], (NetworkId)message.args[1]));
                    }
                    catch (Exception e)
                    {
                        if (_logger.includes.exceptions)
                            _logger.Write($"Failed to relay spawn instruction from {message.caller}. Thrown exception:\n{e}");
                    }
                }
                else if (message.rpcId == RpcId.NETWORK_OBJECT_DESPAWN)
                {
                    try
                    {
                        var bytes = buffer.GetSpan(NetworkSpawner.DespawnByteLength);
                        _spawner.DespawnEncode(bytes, (NetworkId)message.args[0]);
                        if (_logger.includes.rpcCallEncodings)
                            _logger.Write("RELAYING:\n" + _spawner.DespawnEncodingSummary((NetworkId)message.args[0]));
                    }
                    catch (Exception e)
                    {
                        if (_logger.includes.exceptions)
                            _logger.Write($"Failed to relay despawn instruction from {message.caller}. Thrown exception:\n{e}");
                    }
                }
                else
                {
                    try
                    {
                        var bytes = buffer.GetSpan(Protocols.GetRpcByteLength(message.rpcId, message.args));
                        Protocols.EncodeRpc(bytes, message.rpcId, message.caller, message.callee, message.target, message.args);
                        if (_logger.includes.rpcCalls)
                        {
                            var output = $"RELAYING:\n{Protocols.GetRpcName(message.rpcId.Id)} {message.rpcId}, Called on Object {message.target}";
                            if (_logger.includes.rpcCallEncodings)
                                output += ":\n" + Protocols.GetEncodingSummary(message.rpcId, message.caller, message.callee, message.target, message.args);
                            _logger.Write(output);
                        }
                    }
                    catch (Exception e)
                    {
                        if (_logger.includes.exceptions)
                            _logger.Write($"Failed to relay RPC from {message.caller}, sending to {message.callee}. Thrown exception:\n{e}");
                    }
                }
            }
        }

        internal void AddRpc(ClientId callee, RpcId rpcId, NetworkId target, Protocol protocol, object[] args)
        {
            var message = new NetworkBuffer.Message(LocalId, callee, rpcId, target, protocol);

            if (message.rpcId == RpcId.NETWORK_OBJECT_SPAWN)
            {
                try
                {
                    message.bytes = new byte[NetworkSpawner.SpawnByteLength];
                    _spawner.SpawnEncode(message.bytes, (Type)message.args[0], (NetworkId)message.args[1]);
                    if (_logger.includes.rpcCallEncodings)
                        _logger.Write("SENDING:\n" + _spawner.SpawnEncodingSummary((Type)message.args[0], (NetworkId)message.args[1]));
                }
                catch (Exception e)
                {
                    if (_logger.includes.exceptions)
                        _logger.Write($"Failed to encode spawn instruction. Thrown exception:\n{e}");
                }
            }
            else if (message.rpcId == RpcId.NETWORK_OBJECT_DESPAWN)
            {
                try
                {
                    message.bytes = new byte[NetworkSpawner.DespawnByteLength];
                    _spawner.DespawnEncode(message.bytes, (NetworkId)message.args[0]);
                    if (_logger.includes.rpcCallEncodings)
                        _logger.Write("SENDING:\n" + _spawner.DespawnEncodingSummary((NetworkId)message.args[0]));
                }
                catch (Exception e)
                {
                    if (_logger.includes.exceptions)
                        _logger.Write($"Failed to encode despawn instruction. Thrown exception:\n{e}");
                }
            }
            else
            {
                try
                {
                    message.bytes = new byte[Protocols.GetRpcByteLength(rpcId, message.args)];
                    Protocols.EncodeRpc(message.bytes, rpcId, LocalId, callee, target, args);
                    if (_logger.includes.rpcCalls)
                    {
                        var output = $"SENDING:\n{Protocols.GetRpcName(rpcId.Id)} {rpcId}, Called on Object {target}";
                        if (_logger.includes.rpcCallEncodings)
                            output += ":\n" + Protocols.GetEncodingSummary(rpcId, LocalId, callee, target, args);
                        _logger.Write(output);
                    }
                }
                catch (Exception e)
                {
                    if (_logger.includes.exceptions)
                    {
                        var str = new StringBuilder();
                        for (int i = 0; i < args.Length; i++)
                            str.Append($"{i + 1}: {args[i]}\n");
                        _logger.Write($"Failed to encode RPC {rpcId}, with arguments:\n{str}\nThrown exception:\n{e}");
                    }
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
            if (IsActive && _buffer.HasOutgoing)
                _buffer.Send();
        }

        /// <summary>
        /// Ping the target client. A target of <c>ClientId.None</c> will ping the server.
        /// Returns a PingRequest, which is similar to a promise. The ping value will only be known
        /// once the ping request has been resolved.
        /// </summary>
        public PingRequest Ping(ClientId target)
        {
            if (target == LocalId)
            {
                var request = new PingRequest(LocalId, LocalId);
                request.PingReceived();
                request.PingResponded();
                request.PingResolved();
                return request;
            }

            if (target != ClientId.None && !ContainsClient(target))
                throw new ArgumentException("Cannot ping a client that doesn't exist in this session.");
            
            return _buffer.Ping(target);
        }

        /// <summary>
        /// Disconnect the local connection. If this is a server, the server is shut down.
        /// if this is a client, disconnect from the server.
        /// </summary>
        public void Disconnect()
        {
            if (!IsActive)
                return;
            if (_buffer.IsActive)
                _buffer.Disconnect();
            IsActive = false;
            IsReady = false;
            OnLocalDisconnect?.Invoke(LocalId);
            _spawner?.DespawnAll();
        }

        /// <summary>
        /// Disconnect a specific client from the server.
        /// </summary>
        public void Disconnect(ClientId id)
        {
            if (IsClient)
                throw new InvalidOperationException("Only the authority can disconnect other clients.");
            if (Threaded)
                _clientRequests.Enqueue((ConnectionEventType.OnDisconnect, id));
            else
                _buffer.Disconnect(id);
        }

        public void MigrateHost(ClientId id)
        {
            if (IsClient)
                throw new InvalidOperationException("Only the current host or the relay server can initiate a host migration.");
            if (Threaded)
                _clientRequests.Enqueue((ConnectionEventType.OnHostMigration, id));
            else
                _buffer.MigrateHost(id);
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
        /// Iterable of all currently spawned network objects
        /// </summary>
        public IEnumerable<NetworkObject> NetworkObjects => IsRelay ? null : _spawner.NetworkObjects;

        /// <summary>
        /// Try to get an object with the given id. Returns true if one was found, false otherwise.
        /// </summary>
        public bool TryGetObject(NetworkId id, out NetworkObject obj)
        {
            if (IsRelay)
                throw new InvalidOperationException("Relay servers do not manage any state beyond client connections, no network objects exist on this connection.");
            return _spawner.TryGetObject(id, out obj);
        }
        
        /// <summary>
        /// Get an object with the given id. Returns null if none exist.
        /// </summary>
        public NetworkObject GetNetworkObject(NetworkId id)
        {
            if (IsRelay)
                throw new InvalidOperationException("Relay servers do not manage any state beyond client connections, no network objects exist on this connection.");
            return _spawner.GetNetworkObject(id);
        }

        /// <summary>
        /// Spawns a new instance of the given NetworkObject sub-type across all clients.
        /// </summary>
        public T Spawn<T>() where T : NetworkObject, new()
        {
            if (IsClient)
                throw new InvalidOperationException("Clients cannot spawn or destroy network objects.");
            else if (IsRelay)
                throw new InvalidOperationException("Relay servers cannot spawn or destroy network objects.");
            var obj = _spawner.Spawn<T>();
            return obj;
        }

        /// <summary>
        /// Spawns a new instance of the given NetworkObject sub-type across all clients.
        /// </summary>
        public object Spawn(Type t)
        {
            if (IsClient)
                throw new InvalidOperationException("Clients cannot spawn or despawn network objects");
            else if (IsRelay)
                throw new InvalidOperationException("Relay servers cannot spawn or destroy network objects.");
            return _spawner.Spawn(t);
        }

        /// <summary>
        /// Despawns the given NetworkObject across all clients.
        /// </summary>
        public void Despawn(NetworkObject target)
        {
            if (IsClient)
                throw new InvalidOperationException("Clients cannot spawn or despawn network objects");
            else if (IsRelay)
                throw new InvalidOperationException("Relay servers cannot spawn or destroy network objects.");
            _spawner.Despawn(target);
        }
    }
}