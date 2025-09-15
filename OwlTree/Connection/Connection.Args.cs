using System;
using System.Net;

namespace OwlTree
{
    public partial class Connection
    {
        /// <summary>
        /// Initialization arguments for building a new connection.
        /// </summary>
        public class Args
        {
            // socket args

            /// <summary>
            /// Whether this connection is a server or client.<b>Default = Server</b>
            /// </summary>
            public NetRole role = NetRole.Server;
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
            public int udpPort = 9000;
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
            /// Whether or not to automatically shutdown a relay connection if it becomes empty after
            /// all clients disconnect. If false, then the relay must also allow host migration. This is 
            /// controlled with the <c>migratable</c> argument, and will be set to true for you.
            /// <b>Default = true</b>
            /// </summary>
            public bool shutdownWhenEmpty = true;
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
            public ushort owlTreeVersion = Version.Current;

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
            
            /// <summary>
            /// A unique, max 64 ASCII character id used to distinguish different sessions of the same app. <b>Default = "MyAppSession"</b>
            /// </summary>
            public string sessionId = "MyAppSession";

            // buffer transformers

            /// <summary>
            /// Records how much data is being send and received. Adds a read step with a priority of 0, and a send step with a priority of 200.
            /// <b>Default = false</b>
            /// </summary>
            public bool measureBandwidth = false;

            /// <summary>
            /// A callback to output bandwidth recordings.
            /// <b>Default = logger</b>
            /// </summary>
            public Action<Bandwidth> bandwidthReporter = null;

            /// <summary>
            /// Add custom transformers that will be apply to data received from sockets. Steps will be sorted by priority, least to greatest,
            /// and executed in that order. <b>Default = None</b>
            /// </summary>
            public Transformer[] recvSteps = new Transformer[0];

            /// <summary>
            /// Add custom transformers that will be apply to data sent to sockets. Steps will be sorted by priority, least to greatest,
            /// and executed in that order. <b>Default = None</b>
            /// </summary>
            public Transformer[] sendSteps = new Transformer[0];

            /// <summary>
            /// Adds Huffman encoding and decoding to the connection's read and send steps, with a priority of 100. <b>Default = true</b>
            /// </summary>
            public bool useCompression = true;

            // threaded buffer

            /// <summary>
            /// If false, sending and receiving will need to be called by your program with <c>Recv()</c>
            /// and <c>Send()</c>. These operations will be done synchronously.
            /// <br /><br />
            /// If true <b>(Default)</b>, sending and receiving will be handled autonomously in a separate, dedicated thread. 
            /// Receiving will fill a queue of RPCs to be executed in the main program thread by calling <c>ExecuteQueue()</c>.
            /// Sending and receiving will be done at a regular frequency, as defined by the <c>threadUpdateDelta</c> arg.
            /// </summary>
            public bool threaded = true;

            /// <summary>
            /// If the connection is threaded, specify the number of milliseconds the send/recv thread will spend sleeping
            /// between updates. <b>Default = 40 (25 ticks/sec)</b>
            /// </summary>
            public int threadUpdateDelta = 40;

            // simulation buffer

            /// <summary>
            /// Decide how simulation latency and synchronization is handled.
            /// <b>Default = None</b>
            /// </summary>
            public SimulationSystem simulationSystem = SimulationSystem.None;
            /// <summary>
            /// Assumed simulation tick speed in milliseconds. Used to accurately allocate sufficient simulation buffer space.
            /// <c>ExecuteQueue()</c> should called at this rate.
            /// <b>Default = 20 (50 ticks/sec)</b>
            /// </summary>
            public int simulationTickRate = 20;

            /// <summary>
            /// Number of past ticks that will be stored for resimulation. This value only matters if you are using 
            /// a simulation system.
            /// </summary>
            public int pastTickCount = 32;

            // logging

            /// <summary>
            /// Inject a function for outputting logs from the connection. <b>Default = Console.WriteLine</b>
            /// </summary>
            public Logger.Writer logger = Console.WriteLine;

            /// <summary>
            /// Specify what information will get logged. <b>Default = None</b>
            /// </summary>
            public Logger.IncludeRules verbosity = Logger.Includes();

            public Args() { }
        }
    }
}