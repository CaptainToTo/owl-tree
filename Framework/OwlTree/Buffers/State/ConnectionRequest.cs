
using System;

namespace OwlTree
{
    /// <summary>
    /// Sent by clients to request a connection to a server.
    /// </summary>
    public struct ConnectionRequest : IEncodable
    {
        /// <summary>
        /// The AppId associated with this connection.
        /// </summary>
        public AppId appId;
        /// <summary>
        /// True if the connecting client is a host.
        /// </summary>
        public bool isHost;

        public ConnectionRequest(AppId id, bool host)
        {
            appId = id;
            isHost = host;
        }

        public int ByteLength()
        {
            return appId.ByteLength() + 1;
        }

        public static int MaxLength()
        {
            return AppId.MaxLength() + 1;
        }

        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            appId.FromBytes(bytes);
            isHost = bytes[appId.ByteLength()] == 1;
        }

        public void InsertBytes(Span<byte> bytes)
        {
            appId.InsertBytes(bytes);
            bytes[appId.ByteLength()] = (byte)(isHost ? 1 : 0);
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

        public ClientIdAssignment(ClientId assigned, ClientId authority, uint hash)
        {
            assignedId = assigned;
            authorityId = authority;
            assignedHash = hash;
        }

        public ClientIdAssignment(ReadOnlySpan<byte> bytes)
        {
            assignedId = ClientId.None;
            authorityId = ClientId.None;
            assignedHash = 0;
            FromBytes(bytes);
        }

        public static int MaxLength()
        {
            return ClientId.MaxLength() + ClientId.MaxLength() + 4;
        }

        public int ByteLength()
        {
            return assignedId.ByteLength() + authorityId.ByteLength() + 4;
        }

        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            assignedId.FromBytes(bytes);
            authorityId.FromBytes(bytes.Slice(assignedId.ByteLength()));
            assignedHash = BitConverter.ToUInt32(bytes.Slice(assignedId.ByteLength() + authorityId.ByteLength()));
        }

        public void InsertBytes(Span<byte> bytes)
        {
            assignedId.InsertBytes(bytes);
            authorityId.InsertBytes(bytes.Slice(assignedId.ByteLength()));
            BitConverter.TryWriteBytes(bytes.Slice(assignedId.ByteLength() + authorityId.ByteLength()), assignedHash);
        }
    }
}