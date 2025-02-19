
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

        public ConnectionRequest(StringId app, StringId session, bool host)
        {
            appId = app;
            sessionId = session;
            isHost = host;
        }

        public int ByteLength()
        {
            return appId.ByteLength() + sessionId.ByteLength() + 1;
        }

        public static int MaxLength()
        {
            return StringId.MaxByteLength + StringId.MaxByteLength + 1;
        }

        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            appId.FromBytes(bytes);
            sessionId.FromBytes(bytes.Slice(appId.ByteLength()));
            isHost = bytes[appId.ByteLength() + sessionId.ByteLength()] == 1;
        }

        public void InsertBytes(Span<byte> bytes)
        {
            appId.InsertBytes(bytes);
            sessionId.InsertBytes(bytes.Slice(appId.ByteLength()));
            bytes[appId.ByteLength() + sessionId.ByteLength()] = (byte)(isHost ? 1 : 0);
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
        /// the server is currently at max capacity.
        /// </summary>
        ServerFull,
        /// <summary>
        /// The client's connection request was rejected because 
        /// the provided app id doesn't match the server's.
        /// </summary>
        IncorrectAppId,
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

        public ClientIdAssignment(ClientId assigned, ClientId authority, uint hash, int maxClients)
        {
            assignedId = assigned;
            authorityId = authority;
            assignedHash = hash;
            this.maxClients = maxClients;
        }

        public ClientIdAssignment(ReadOnlySpan<byte> bytes)
        {
            assignedId = ClientId.None;
            authorityId = ClientId.None;
            assignedHash = 0;
            maxClients = int.MaxValue;
            FromBytes(bytes);
        }

        public static int MaxLength()
        {
            return ClientId.MaxByteLength + ClientId.MaxByteLength + 4 + 4;
        }

        public int ByteLength()
        {
            return assignedId.ByteLength() + authorityId.ByteLength() + 4 + 4;
        }

        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            assignedId.FromBytes(bytes);
            authorityId.FromBytes(bytes.Slice(assignedId.ByteLength()));
            assignedHash = BitConverter.ToUInt32(bytes.Slice(assignedId.ByteLength() + authorityId.ByteLength()));
            maxClients = BitConverter.ToInt32(bytes.Slice(assignedId.ByteLength() + authorityId.ByteLength() + 4));
        }

        public void InsertBytes(Span<byte> bytes)
        {
            assignedId.InsertBytes(bytes);
            authorityId.InsertBytes(bytes.Slice(assignedId.ByteLength()));
            BitConverter.TryWriteBytes(bytes.Slice(assignedId.ByteLength() + authorityId.ByteLength()), assignedHash);
            BitConverter.TryWriteBytes(bytes.Slice(assignedId.ByteLength() + authorityId.ByteLength() + 4), maxClients);
        }
    }
}