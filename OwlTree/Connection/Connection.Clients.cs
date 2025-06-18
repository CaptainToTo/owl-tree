using System;
using System.Collections.Generic;

namespace OwlTree
{
    public partial class Connection
    {
        // mirrors buffer state, but on the main thread
        private List<ClientId> _clients = new List<ClientId>();

        /// <summary>
        /// The number of connected clients.
        /// </summary>
        public int ClientCount => _clients.Count;

        /// <summary>
        /// The maximum number of clients allowed to be connected at once in this session.
        /// This value will not be accurate on clients until the connection is ready.
        /// </summary>
        public int MaxClients => _buffer.MaxClients;

        /// <summary>
        /// Iterable of all connected clients.
        /// </summary>
        public IEnumerable<ClientId> Clients => _clients;

        /// <summary>
        /// Returns true if the given client id currently exists in this session.
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
        /// Invoked when the authority is migrated. Provides the new authority's client id.
        /// </summary>
        public event ClientId.Delegate OnHostMigration;

        /// <summary>
        /// The client id assigned to this local instance. Servers will have a LocalId of <c>ClientId.None</c>.
        /// </summary>
        public ClientId LocalId => IsReady ? _buffer.LocalId : ClientId.None;

        /// <summary>
        /// the client id of the instance assigned as the authority of the session. 
        /// Servers will have an id of <c>ClientId.None</c>.
        /// </summary>
        public ClientId Authority { get; private set; } = ClientId.None;

        /// <summary>
        /// Returns true if the local connection is the authority of this session.
        /// </summary>
        public bool IsAuthority => !IsRelay && LocalId == Authority;

        /// <summary>
        /// Disconnect the local connection. If this is a server, the server is shut down.
        /// if this is a client, disconnect from the server.
        /// If connection is threaded, then connection will not immediately become inactive
        /// as resources are cleaned up. Listen to OnLocalDisconnect event for when the 
        /// connection becomes inactive.
        /// </summary>
        public void Disconnect()
        {
            if (!IsActive || !_buffer.IsActive)
                return;
            else if (Threaded)
                _buffer.SendDisconnectSignal();
            else
            {
                _buffer.Disconnect();
                IsActive = false;
                IsReady = false;
                OnLocalDisconnect?.Invoke(LocalId);
                _spawner?.DespawnAll();
            }
        }

        /// <summary>
        /// Disconnect a specific client from the server.
        /// This can only be called by the authority.
        /// </summary>
        public void Disconnect(ClientId id)
        {
            if (IsClient)
                throw new InvalidOperationException("Only the authority can disconnect other clients.");
            if (Threaded)
            {
                _simBuffer.AddOutgoing(new OutgoingMessage
                {
                    tick = LocalTick,
                    rpcId = new RpcId(RpcId.ClientDisconnectedId),
                    callee = id
                });
            }
            else
                _buffer.Disconnect(id);
        }

        /// <summary>
        /// Reassign the authority client (a.k.a. host) to a new client.
        /// This can only be called by the authority.
        /// </summary>
        public void MigrateHost(ClientId id)
        {
            if (IsClient)
                throw new InvalidOperationException("Only the current host or the relay server can initiate a host migration.");
            if (IsServer)
                throw new InvalidOperationException("Server authoritative sessions cannot have authority migrated off of the server.");
            if (Threaded)
            {
                _simBuffer.AddOutgoing(new OutgoingMessage
                {
                    tick = LocalTick,
                    rpcId = new RpcId(RpcId.HostMigrationId),
                    callee = id
                });
            }
            else
                _buffer.MigrateHost(id);
        }

        /// <summary>
        /// Ping the target client. A target of <c>ClientId.None</c> will ping the server.
        /// Returns a PingRequest, which is similar to a promise. The ping value will only be known
        /// once the ping request has been resolved.
        /// </summary>
        public PingRequest Ping(ClientId target, Protocol protocol = Protocol.Udp)
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

            return _buffer.Ping(target, protocol);
        }

        internal PingRequest TestPing(ClientId target)
        {
            return _buffer.Ping(target);
        }

        // * RPC HANDLERS

        // client id associated with event placed in caller prop
        private void HandleClientEvent(IncomingMessage m)
        {
            try
            {
                switch (m.rpcId)
                {
                    case RpcId.ClientConnectedId:
                        AddClient(m);
                        break;

                    case RpcId.ClientDisconnectedId:
                        RemoveClient(m);
                        break;

                    case RpcId.LocalReadyId:
                        LocalReady(m);
                        break;

                    case RpcId.HostMigrationId:
                        MigrateHost(m);
                        break;

                    case RpcId.ConnectionRejectedId:
                        IsActive = false;
                        OnConnectionRejected?.Invoke((ConnectionResponseCode)m.args[0]);
                        break;
                }
            }
            catch (Exception e)
            {
                if (Logger.includes.exceptions)
                    Logger.Write($"Failed to dispatch client event {RpcId.SpecialIdToString(m.rpcId)}. Exception Thrown:\n{e}");
            }
        }

        private void AddClient(IncomingMessage m)
        {
            if (Logger.includes.clientEvents)
                Logger.Write("New client connected: " + m.caller.ToString());

            if (IsAuthority)
                _spawner.SendNetworkObjects(m.caller);
            else if (IsRelay && m.caller == _buffer.Authority)
            {
                Authority = m.caller;
                if (Logger.includes.clientEvents)
                    Logger.Write("Host client has been assigned to: " + m.caller.ToString());
            }

            // clients in server auth sessions only receive sim updates from the server
            if (IsRelayed || IsServer)
                _simBuffer.AddTickSource(m.caller);

            _clients.Add(m.caller);
            OnClientConnected?.Invoke(m.caller);
        }

        private void RemoveClient(IncomingMessage m)
        {
            if (m.caller == LocalId)
            {
                if (Logger.includes.clientEvents)
                    Logger.Write(IsServer || IsRelay ? "Local server shutdown." : "Local client disconnected.");
                IsActive = false;
                IsReady = false;
                OnLocalDisconnect?.Invoke(LocalId);
                _spawner?.DespawnAll();
            }
            else
            {
                if (Logger.includes.clientEvents)
                    Logger.Write("Remote client disconnected: " + m.caller.ToString());
                if (IsRelayed || IsServer)
                    _simBuffer.RemoveTickSource(m.caller);
                _clients.Remove(m.caller);
                OnClientDisconnected?.Invoke(m.caller);
            }
        }

        private void LocalReady(IncomingMessage m)
        {
            IsReady = true;
            Authority = _buffer.Authority;
            if (Logger.includes.clientEvents)
                Logger.Write($"Connection is ready. Local client id is: {LocalId}, authority id is: {Authority}");
            if (LocalId == Authority && IsClient)
            {
                NetRole = NetRole.Host;
                if (Logger.includes.clientEvents)
                    Logger.Write("Local client assigned as host, this connection now has authority privileges.");
            }
            else if (LocalId != Authority && IsHost)
            {
                NetRole = NetRole.Client;
                if (Logger.includes.clientEvents)
                    Logger.Write("Local connection requested to be host, but has been downgraded to client. Authority privileges removed.");
            }
            _simBuffer.InitBuffer(TickRate, Latency, 0, LocalId, Authority);
            if (IsServerAuthoritative)
                _simBuffer.AddTickSource(ClientId.None);
            if (!IsServer && !IsRelay)
                _clients.Add(m.caller);
            OnReady?.Invoke(m.caller);
        }

        private void MigrateHost(IncomingMessage m)
        {
            Authority = m.caller;
            if (NetRole == NetRole.Host && Authority != LocalId)
                NetRole = NetRole.Client;
            if (NetRole != NetRole.Relay && m.caller == LocalId)
                NetRole = NetRole.Host;
            if (Logger.includes.clientEvents)
                Logger.Write("Host migrated, new authority is: " + m.caller.ToString());
            _simBuffer.UpdateAuthority(Authority);
            OnHostMigration?.Invoke(m.caller);
        }

        private void ResolvePing(IncomingMessage message)
        {
            var request = (PingRequest)message.args[0];
            if (Logger.includes.pings)
                Logger.Write("Resolved ping request: " + request.ToString());
            request.PingResolved();
        }
    }
}