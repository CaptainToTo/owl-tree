
using System;

namespace OwlTree
{
    /// <summary>
    /// Sent by clients to request a connection to a server.
    /// </summary>
    public struct ConnectionRequest : IEncodable
    {
        /// <summary>
        /// The app id associated with this connection.
        /// </summary>
        public StringId appId;
        /// <summary>
        /// The session id associated with this connection.
        /// </summary>
        public StringId sessionId;
        /// <summary>
        /// True if the connecting client is a host.
        /// </summary>
        public bool isHost;

        /// <summary>
        /// The simulation system the client connection is using.
        /// </summary>
        public SimulationSystem simulationSystem;

        /// <summary>
        /// The session uniform tick rate all simulations must adhere to.
        /// </summary>
        public int tickRate;

        public ConnectionRequest(StringId app, StringId session, bool host, SimulationSystem simSystem, int tickSpeed)
        {
            appId = app;
            sessionId = session;
            isHost = host;
            simulationSystem = simSystem;
            tickRate = tickSpeed;
        }

        public int ByteLength()
        {
            return appId.ByteLength() + sessionId.ByteLength() + 2 + 4;
        }

        public static int MaxLength()
        {
            return StringId.MaxByteLength + StringId.MaxByteLength + 2 + 4;
        }

        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            appId.FromBytes(bytes);
            sessionId.FromBytes(bytes.Slice(appId.ByteLength()));
            isHost = bytes[appId.ByteLength() + sessionId.ByteLength()] == 1;
            simulationSystem = (SimulationSystem)bytes[appId.ByteLength() + sessionId.ByteLength() + 1];
            tickRate = Encoder.DecodeInt32(bytes.Slice(appId.ByteLength() + sessionId.ByteLength() + 2));
        }

        public void InsertBytes(Span<byte> bytes)
        {
            appId.InsertBytes(bytes);
            sessionId.InsertBytes(bytes.Slice(appId.ByteLength()));
            bytes[appId.ByteLength() + sessionId.ByteLength()] = (byte)(isHost ? 1 : 0);
            bytes[appId.ByteLength() + sessionId.ByteLength() + 1] = (byte) simulationSystem;
            Encoder.InsertBytes(bytes.Slice(appId.ByteLength() + sessionId.ByteLength() + 2), tickRate);
        }
    }

    /// <summary>
    /// Responses servers can give to clients sending connection requests via UDP.
    /// </summary>
    public enum ConnectionResponseCode
    {
        /// <summary>
        /// The client's connection request was accepted.
        /// They can now make the TCP handshake.
        /// </summary>
        Accepted,
        /// <summary>
        /// The client's connection request was rejected because 
        /// the session is currently at max capacity.
        /// </summary>
        SessionFull,
        /// <summary>
        /// The client's connection request was rejected because 
        /// the provided app id doesn't match the server's.
        /// </summary>
        IncorrectAppId,
        /// <summary>
        /// The client's connection request was rejected because 
        /// the provided session id doesn't match the server's.
        /// </summary>
        IncorrectSessionId,
        /// <summary>
        /// The client's connection request was rejected because
        /// the provided simulation buffer control system doesn't match the server's.
        /// </summary>
        IncorrectSimulationControl,
        /// <summary>
        /// The client's connection request was rejected because
        /// they claimed to be the host, but the session already has one assigned.
        /// </summary>
        HostAlreadyAssigned,
        /// <summary>
        /// Catch all response for rejecting a client's connection request.
        /// </summary>
        Rejected
    }
    
    /// <summary>
    /// Basic delegate for handling connection response codes on clients.
    /// </summary>
    public delegate void ConnectionResponseHandler(ConnectionResponseCode response);

    /// <summary>
    /// Sent to clients on connecting to the server to assign the local id.
    /// </summary>
    public struct ClientIdAssignment : IEncodable
    {
        /// <summary>
        /// The assigned local id for the client.
        /// </summary>
        public ClientId assignedId;

        /// <summary>
        /// The id of the authority of the session.
        /// </summary>
        public ClientId authorityId;

        /// <summary>
        /// The unique 32 bit integer assigned to this client, which is kept secret between the
        /// server and client.
        /// </summary>
        public uint assignedHash;

        /// <summary>
        /// The max number of clients allowed in this session at once.
        /// </summary>
        public int maxClients;

        /// <summary>
        /// Whether or not the authority of a relayed peer-to-peer session (the host) can be reassigned.
        /// </summary>
        public bool migratable;

        /// <summary>
        /// Whether or not the session will be shutdown once all clients disconnect.
        /// </summary>
        public bool shutdownWhenEmpty;

        public ClientIdAssignment(ClientId assigned, ClientId authority, uint hash, int maxClients, bool migratable, bool shutdownWhenEmpty)
        {
            assignedId = assigned;
            authorityId = authority;
            assignedHash = hash;
            this.maxClients = maxClients;
            this.migratable = migratable;
            this.shutdownWhenEmpty = shutdownWhenEmpty;
        }

        public ClientIdAssignment(ReadOnlySpan<byte> bytes)
        {
            assignedId = ClientId.None;
            authorityId = ClientId.None;
            assignedHash = 0;
            maxClients = int.MaxValue;
            migratable = false;
            shutdownWhenEmpty = true;
            FromBytes(bytes);
        }

        public static int MaxLength()
        {
            return ClientId.MaxByteLength + ClientId.MaxByteLength + 4 + 4 + 2;
        }

        public int ByteLength()
        {
            return assignedId.ByteLength() + authorityId.ByteLength() + 4 + 4 + 2;
        }

        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            assignedId.FromBytes(bytes);
            authorityId.FromBytes(bytes.Slice(assignedId.ByteLength()));
            assignedHash = Encoder.DecodeUInt32(bytes.Slice(assignedId.ByteLength() + authorityId.ByteLength()));
            maxClients = Encoder.DecodeInt32(bytes.Slice(assignedId.ByteLength() + authorityId.ByteLength() + 4));
            migratable = bytes[assignedId.ByteLength() + authorityId.ByteLength() + 4 + 4] == 0 ? false : true;
            shutdownWhenEmpty = bytes[assignedId.ByteLength() + authorityId.ByteLength() + 4 + 5] == 0 ? false : true;
        }

        public void InsertBytes(Span<byte> bytes)
        {
            assignedId.InsertBytes(bytes);
            authorityId.InsertBytes(bytes.Slice(assignedId.ByteLength()));
            Encoder.InsertBytes(bytes.Slice(assignedId.ByteLength() + authorityId.ByteLength()), assignedHash);
            Encoder.InsertBytes(bytes.Slice(assignedId.ByteLength() + authorityId.ByteLength() + 4), maxClients);
            bytes[assignedId.ByteLength() + authorityId.ByteLength() + 4 + 4] = (byte)(migratable ? 1 : 0);
            bytes[assignedId.ByteLength() + authorityId.ByteLength() + 4 + 5] = (byte)(shutdownWhenEmpty ? 1 : 0);
        }
    }
}