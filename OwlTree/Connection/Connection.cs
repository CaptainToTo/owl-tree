
using System;
using System.Threading;

namespace OwlTree
{
    /// <summary>
    /// Determines the responsibilities and capabilities a Connection will have.
    /// </summary>
    public enum NetRole
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
    /// How the simulation buffer will be handled in the session.
    /// </summary>
    public enum SimulationSystem
    {
        /// <summary>
        /// No simulation buffer will be maintained. This means the session will not maintain a synchronized simulation tick number.
        /// Alignment is not considered. Best for games with irregular tick timings like turn-based games.
        /// </summary>
        None,
        /// <summary>
        /// Wait for all clients to deliver their input before executing the next tick. Simulation buffer is only maintained for ticks
        /// that haven't been run yet.
        /// </summary>
        Lockstep,
        /// <summary>
        /// Maintain a simulation buffer of received future ticks, and past ticks. When receiving updates from a previous tick,
        /// re-simulate from the new information back to the current tick.
        /// </summary>
        Rollback,
        /// <summary>
        /// Maintain a simulation buffer of received future ticks.
        /// </summary>
        Snapshot
    }

    /// <summary>
    /// Primary interface for OwlTree server-client connections. 
    /// </summary>
    public partial class Connection
    {
        /// <summary>
        /// Create a new connection. Provide a <c>Connection.Args</c> instance to configure this connection.
        /// Once the connection ends, it cannot be reused.
        /// </summary>
        public Connection(Args args)
        {
            NetRole = args.role;

            Logger = new Logger(args.logger, args.verbosity);

            Protocols = IsRelay ? null : RpcProtocols.GetProjectImplementation();

            if (Protocols == null && !IsRelay)
                Logger.Write("WARNING: No project RPC protocols found. Ensure the OwlTree source generator is included in your project properly.");

            if (Protocols != null && Logger.includes.allRpcProtocols)
                Logger.Write(Protocols.GetAllProtocolSummaries());

            if (IsRelay)
            {
                _simBuffer = new MessageQueue(Logger);
            }
            else
            {
                switch (args.simulationSystem)
                {
                    case SimulationSystem.Lockstep:
                        _simBuffer = new Lockstep(Logger);
                        break;
                    case SimulationSystem.Rollback:
                        _simBuffer = new Rollback(Logger);
                        break;
                    case SimulationSystem.Snapshot:
                        _simBuffer = new Snapshot(Logger);
                        break;
                    case SimulationSystem.None:
                    default:
                        _simBuffer = new MessageQueue(Logger);
                        break;
                }
                _simBuffer.OnResimulation = (tick) => OnResimulation?.Invoke(tick);
            }
            SimulationSystem = args.simulationSystem;
            TickRate = args.simulationTickRate;

            NetworkBuffer.Args bufferArgs = new NetworkBuffer.Args(){
                owlTreeVer = args.owlTreeVersion,
                minOwlTreeVer = args.minOwlTreeVersion,
                appVer = args.appVersion,
                minAppVer = args.minAppVersion,
                appId = args.appId,
                sessionId = args.sessionId,
                migratable = args.migratable,
                shutdownWhenEmpty = args.shutdownWhenEmpty,
                addr = args.serverAddr,
                serverTcpPort = args.tcpPort,
                serverUdpPort = args.udpPort,
                bufferSize = args.bufferSize,
                incomingDecoder = DecodeIncoming,
                messageQueue = _simBuffer,
                logger = Logger,
                simulationSystem = args.simulationSystem,
                tickRate = TickRate
            };

            switch (args.role)
            {
                case NetRole.Server:
                    _buffer = new ServerBuffer(bufferArgs, args.maxClients, args.connectionRequestTimeout, args.whitelist);
                    IsReady = true;
                    break;
                case NetRole.Relay:
                    _buffer = new RelayBuffer(bufferArgs, args.maxClients, args.connectionRequestTimeout, args.hostAddr, args.whitelist);
                    IsReady = true;
                    break;
                case NetRole.Client:
                case NetRole.Host:
                    _buffer = new ClientBuffer(bufferArgs, args.connectionRequestRate, args.connectionRequestLimit, IsHost);
                    IsReady = false;
                    break;
            }
            IsActive = true;

            if (!IsRelay)
            {
                var factory = ProxyFactory.GetProjectImplementation();

                if (factory == null)
                    Logger.Write("WARNING: No project network object factory found. Ensure the OwlTree source generator is included in your project properly.");

                if (factory != null && Logger.includes.allTypeIds)
                    Logger.Write(factory.GetAllIdAssignments());

                _spawner = new NetworkSpawner(this, factory);

                _spawner.OnObjectSpawn = (obj) => {
                    if (Logger.includes.spawnEvents)
                        Logger.Write("Spawned new network object: " + obj.Id.ToString() + ", of type: " + obj.GetType().ToString());
                    OnObjectSpawn?.Invoke(obj);
                };
                _spawner.OnObjectDespawn = (obj) => {
                    if (Logger.includes.spawnEvents)
                        Logger.Write("Despawned network object: " + obj.Id.ToString() + ", of type: " + obj.GetType().ToString());
                    OnObjectDespawn?.Invoke(obj);
                };
            }

            _buffer.AddRecvStep(new Transformer{
                priority = 100,
                step = Huffman.Decode
            });
            if (args.useCompression)
            {
                _buffer.AddSendStep(new Transformer{
                    priority = 100,
                    step = Huffman.Encode
                });
            }

            if (args.measureBandwidth)
            {
                Bandwidth = new Bandwidth(args.bandwidthReporter == null ? (b) => {
                    var str = $"Bandwidth report at {Timestamp.NowString}:\n";
                    str += $"   Incoming: {b.IncomingKbPerSecond()} KB/s\n";
                    str += $"   Outgoing: {b.OutgoingKbPerSecond()} KB/s";
                    Logger.Write(str);
                } : args.bandwidthReporter);

                _buffer.AddRecvStep(new Transformer{
                    priority = 0,
                    step = Bandwidth.RecordIncoming
                });

                _buffer.AddSendStep(new Transformer{
                    priority = 200,
                    step = Bandwidth.RecordOutgoing
                });
            }

            foreach (var step in args.recvSteps)
                _buffer.AddRecvStep(step);
            foreach (var step in args.sendSteps)
                _buffer.AddSendStep(step);

            if (args.threaded)
            {
                Threaded = true;
                _threadUpdateDelta = args.threadUpdateDelta;
                _bufferThread = new Thread(NetworkLoop);
                _bufferThread.Start();
            }
        }

        /// <summary>
        /// Access metadata about RPC encodings and generated protocols.
        /// </summary>
        public readonly RpcProtocols Protocols;

        /// <summary>
        /// The logger used by this connection.
        /// </summary>
        internal readonly Logger Logger;

        /// <summary>
        /// Uses this connection's logger to output a message.
        /// </summary>
        public void Log(string message) => Logger.Write(message);

        /// <summary>
        /// Access bandwidth data about this connection. If <c>measureBandwidth</c>
        /// was not enabled during configuration, then this will be null.
        /// </summary>
        public readonly Bandwidth Bandwidth = null;


        /// <summary>
        /// Whether or not this connection is using a send/recv thread.
        /// </summary>
        public readonly bool Threaded = false;
        private int _threadUpdateDelta = 40;
        private Thread _bufferThread = null;

        // run by send/recv thread
        private void NetworkLoop()
        {
            // try to connect if client
            while (!_buffer.IsReady && _buffer.IsActive)
            {
                _buffer.Recv();
                Thread.Sleep(_threadUpdateDelta);
            }
            // recv and send until this connection ends
            while (_buffer.IsActive && IsActive)
            {
                long start = Timestamp.Now;
                
                try
                {
                    _buffer.Recv();
                }
                catch (Exception e)
                {
                    if (Logger.includes.exceptions)
                        Logger.WriteError("Failed during receive in network thread. Connection will be closed.", e);
                    _buffer.Disconnect();
                }

                if (!_buffer.IsActive)
                    break;

                if (_simBuffer.HasOutgoing() || _buffer.HasOutgoing)
                {
                    try
                    {
                        _buffer.Send();
                    }
                    catch (Exception e)
                    {
                        if (Logger.includes.exceptions)
                            Logger.WriteError("Failed during send in network thread. Connection will be closed.", e);
                        _buffer.Disconnect();
                        break;
                    }
                }

                if (!_buffer.IsActive)
                    break;
                
                long diff = Timestamp.Now - start;

                Thread.Sleep(Math.Max(0, _threadUpdateDelta - (int)diff));
            }
            _buffer.Disconnect();
        }

        /// <summary>
        /// Whether this connection represents a server or client.
        /// </summary>
        public NetRole NetRole { get; private set; }

        /// <summary>
        /// Whether this connection is active. Will be false for clients if 
        /// they have been disconnected from the server.
        /// </summary>
        public bool IsActive { get; private set; } = false;

        /// <summary>
        /// Whether this connection has established a link to the server. 
        /// This is true for clients once they've been assigned a local id.
        /// </summary>
        public bool IsReady { get; private set; } = false;

        /// <summary>
        /// Receive any packets that have been sent to this connection. Execute them with <c>ExecuteQueue()</c>.
        /// This can only be called on non-threaded connections.
        /// </summary>
        public void Recv()
        {
            if (Threaded)
                throw new InvalidOperationException("Cannot perform read operation on a threaded connection. This is handled for you in a dedicated thread.");
            if (IsActive)
                _buffer.Recv();
        }

        /// <summary>
        /// Block until the connection is ready.
        /// This can only be called on non-threaded connections.
        /// </summary>
        public void AwaitConnection()
        {
            if (Threaded)
                throw new InvalidOperationException("Cannot perform await connection operation on a threaded connection. This is handled for you in a dedicated thread.");
            while (!_buffer.IsReady && _buffer.IsActive)
            {
                _buffer.Recv();
                Thread.Sleep(_threadUpdateDelta);
            }
        }

        /// <summary>
        /// Execute any RPCs that have been received in the last <c>Recv()</c> call.
        /// </summary>
        public void ExecuteQueue()
        {
            if (!IsActive)
                return;


            while (_simBuffer.TryGetNextIncoming(out var message))
            {
                if (message.rpcId.IsClientEvent())
                {
                    HandleClientEvent(message);
                }
                else if (NetRole == NetRole.Client && message.rpcId.IsObjectEvent())
                {
                    HandleSpawnerMessage(message);
                }
                else if (message.rpcId == RpcId.PingRequestId)
                {
                    ResolvePing(message);
                }
                else if (TryGetObject(message.target, out var target))
                {
                    InvokeRpc(message, target);
                }

                // if local connection was disconnected in a client event received, exit
                if (!IsActive)
                    return;
            }

            SearchForObjects();

            _simBuffer.NextTick();
        }

        /// <summary>
        /// Send current outgoing packets.
        /// This can only be called on non-threaded connections.
        /// </summary>
        public void Send()
        {
            if (Threaded)
                throw new InvalidOperationException("Cannot perform send operation on a threaded connection. This is handled for you in a dedicated thread.");
            if (IsActive && _buffer.HasOutgoing)
                _buffer.Send();
        }
    }
}