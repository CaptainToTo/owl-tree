using System;
using System.Threading;

namespace OwlTree
{
    /// <summary>
    /// Thread safe logger that filters which type of outputs get written based on the 
    /// selected verbosity. Provide a Printer function that the logger can use when trying to write.
    /// </summary>
    public class Logger
    {
        /// <summary>
        /// Function signature for what the logger will call to write.
        /// Provide in the constructor.
        /// </summary>
        public delegate void Printer(string text);

        /// <summary>
        /// Create a new set of logger include rules.
        /// </summary>
        public static IncludeRules Includes() => new IncludeRules();

        /// <summary>
        /// Specifies what types of information should be output by the logger.
        /// </summary>
        public struct IncludeRules
        {
            public bool spawnEvents { get; private set; }

            /// <summary>
            /// Output when a NetworkObject is spawned or despawned.
            /// </summary>
            public IncludeRules SpawnEvents()
            {
                spawnEvents = true;
                return this;
            }

            public bool clientEvents { get; private set; }

            /// <summary>
            /// Output when a client connects or disconnects.
            /// </summary>
            public IncludeRules ClientEvents()
            {
                clientEvents = true;
                return this;
            }

            public bool connectionAttempts { get; private set; }

            /// <summary>
            /// Output any connection attempts if this connection is a server.
            /// </summary>
            public IncludeRules ConnectionAttempts()
            {
                connectionAttempts = true;
                return this;
            }

            public bool allTypeIds { get; private set; }

            /// <summary>
            /// On creating this connection, output all of the NetworkObject type ids it is aware of.
            /// </summary>
            public IncludeRules AllTypeIds()
            {
                allTypeIds = true;
                return this;
            }

            public bool allRpcProtocols { get; private set; }

            /// <summary>
            /// On creating this connection, output all of the RPC protocols it is aware of.
            /// </summary>
            public IncludeRules AllRpcProtocols()
            {
                allRpcProtocols = true;
                return this;
            }

            public bool rpcCalls { get; private set; }

            /// <summary>
            /// Output when an RPC is called on the local connection.
            /// </summary>
            public IncludeRules RpcCalls()
            {
                rpcCalls = true;
                return this;
            }

            public bool rpcReceives { get; private set; }

            /// <summary>
            /// Output when an RPC call is received.
            /// </summary>
            public IncludeRules RpcReceives()
            {
                rpcReceives = true;
                return this;
            }

            public bool rpcCallEncodings { get; private set; }

            /// <summary>
            /// Output the argument byte encodings of called RPCs.
            /// </summary>
            public IncludeRules RpcCallEncodings()
            {
                rpcCalls = true;
                rpcCallEncodings = true;
                return this;
            }

            public bool rpcReceiveEncodings { get; private set; }

            /// <summary>
            /// Output the argument byte encodings received on incoming RPC calls.
            /// </summary>
            public IncludeRules RpcReceiveEncodings()
            {
                rpcReceiveEncodings = true;
                return this;
            }

            public bool tcpPreTransform { get; private set; }
            
            /// <summary>
            /// Output TCP packets in full, before any transformer steps are applied.
            /// </summary>
            public IncludeRules TcpPreTransform()
            {
                tcpPreTransform = true;
                return this;
            }

            public bool tcpPostTransform { get; private set; }

            /// <summary>
            /// Output TCP packets in full, after all transformer steps are applied.
            /// </summary>
            public IncludeRules TcpPostTransform()
            {
                tcpPostTransform = true;
                return this;
            }

            public bool udpPreTransform { get; private set; }

            /// <summary>
            /// Output UDP packets in full, before any transformer steps are applied.
            /// </summary>
            public IncludeRules UdpPreTransform()
            {
                udpPreTransform = true;
                return this;
            }

            public bool udpPostTransform { get; private set; }

            /// <summary>
            /// Output UDP packets in full, after all transformer steps are applied.
            /// </summary>
            public IncludeRules UdpPostTransform()
            {
                udpPostTransform = true;
                return this;
            }
        }

        /// <summary>
        /// Create a new logger, that will use the provided Printer for writing logs,
        /// and will only log output that passes the given verbosity.
        /// </summary>
        public Logger(Printer printer, IncludeRules rules)
        {
            _printer = printer;
            includes = rules;
        }

        private Printer _printer;
        public IncludeRules includes { get; private set; }

        private Mutex _lock = new Mutex();

        /// <summary>
        /// Write a log. This is thread safe, and will block if another thread is currently using the same logger.
        /// </summary>
        public void Write(string text)
        {
            _lock.WaitOne();
            _printer.Invoke(text);
            _lock.ReleaseMutex();
        }
    }
}