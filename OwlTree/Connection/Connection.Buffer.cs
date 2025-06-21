namespace OwlTree
{
    public partial class Connection
    {
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
        /// Invoked when the local client connection is rejected by the server.
        /// </summary>
        public event ConnectionResponseHandler OnConnectionRejected;

        private NetworkBuffer _buffer;

        /// <summary>
        /// Returns true if this connection is configured to be a server.
        /// </summary>
        public bool IsServer => NetRole == NetRole.Server; 

        /// <summary>
        /// Returns true if this connection is configured to be a client.
        /// </summary>
        public bool IsClient => NetRole == NetRole.Client; 

        /// <summary>
        /// Returns true if this connection is configured to be a host client.
        /// </summary>
        public bool IsHost => NetRole == NetRole.Host; 

        /// <summary>
        /// Returns true if this connection is configured to be a relay server.
        /// </summary>
        public bool IsRelay => NetRole == NetRole.Relay; 

        /// <summary>
        /// Returns true if this session is server authoritative.
        /// </summary>
        public bool IsServerAuthoritative => Authority == ClientId.None;

        /// <summary>
        /// Returns true if this session is relayed peer-to-peer.
        /// </summary>
        public bool IsRelayed => Authority != ClientId.None;

        /// <summary>
        /// Returns true if the current session supports host migration.
        /// This can only be the case for relayed sessions.
        /// </summary>
        public bool Migratable => _buffer.Migratable;

        /// <summary>
        /// Returns true if this session will automatically end once all
        /// clients disconnect, closing the server connection.
        /// </summary>
        public bool ShutdownWhenEmpty => _buffer.ShutdownWhenEmpty;

        /// <summary>
        /// The TCP port the server connection managing this session is listening to.
        /// </summary>
        public int ServerTcpPort => _buffer.ServerTcpPort;
        /// <summary>
        /// The UDP port this server connection managing this session is listening to.
        /// </summary>
        public int ServerUdpPort => _buffer.ServerUdpPort;
        /// <summary>
        /// The local TCP port this connection is listening to.
        /// </summary>
        public int LocalTcpPort => _buffer.LocalTcpPort();
        /// <summary>
        /// The local UDP port this connection is listening to.
        /// </summary>
        public int LocalUdpPort => _buffer.LocalUdpPort();
        /// <summary>
        /// The general packet millisecond latency of the connection. Servers report the worst latency among all connected clients. 
        /// Latency measures the transit time of packets received, not ping which is round-trip time.
        /// </summary>
        public int Latency => _buffer.Latency();

        /// <summary>
        /// The app this connection is associated with.
        /// </summary>
        public StringId AppId => _buffer.ApplicationId;
        /// <summary>
        /// The session this connection is associated with.
        /// </summary>
        public StringId SessionId => _buffer.SessionId;
    }
}