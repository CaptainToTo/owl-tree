using System;
using System.Text;

namespace OwlTree
{
    public static partial class Encoder
    {
        // * Network Buffer Message Protocols

        /// <summary>
        /// The number of bytes required to encode client events.
        /// </summary>
        internal static int ClientMessageLength => RpcId.MaxByteLength + ClientId.MaxByteLength;

        /// <summary>
        /// The number of bytes required to encode the local client connected event.
        /// </summary>
        internal static int LocalClientConnectLength => RpcId.MaxByteLength + ClientIdAssignment.MaxLength();

        /// <summary>
        /// The number of bytes required to encode a ping request.
        /// </summary>
        internal static int PingRequestLength => RpcId.MaxByteLength + PingRequest.MaxLength();

        internal static void ClientConnectEncode(Span<byte> bytes, ClientId id)
        {
            var rpcId = new RpcId(RpcId.ClientConnectedId);
            var ind = rpcId.ByteLength();
            rpcId.InsertBytes(bytes.Slice(0, ind));
            id.InsertBytes(bytes.Slice(ind, id.ByteLength()));
        }

        internal static void LocalClientConnectEncode(Span<byte> bytes, ClientIdAssignment assignment)
        {
            var rpcId = new RpcId(RpcId.LocalClientConnectedId);
            var ind = rpcId.ByteLength();
            rpcId.InsertBytes(bytes.Slice(0, ind));
            assignment.InsertBytes(bytes.Slice(ind));
        }

        internal static void ClientDisconnectEncode(Span<byte> bytes, ClientId id)
        {
            var rpcId = new RpcId(RpcId.ClientDisconnectedId);
            var ind = rpcId.ByteLength();
            rpcId.InsertBytes(bytes.Slice(0, ind));
            id.InsertBytes(bytes.Slice(ind, id.ByteLength()));
        }

        internal static void ConnectionRequestEncode(Packet packet, ConnectionRequest request)
        {
            var bytes = packet.GetSpan(RpcId.MaxByteLength + request.ByteLength());
            var rpc = new RpcId(RpcId.ConnectionRequestId);
            rpc.InsertBytes(bytes);
            request.InsertBytes(bytes.Slice(rpc.ByteLength()));
        }

        internal static void HostMigrationEncode(Span<byte> bytes, ClientId newHost)
        {
            var rpcId = new RpcId(RpcId.HostMigrationId);
            var ind = rpcId.ByteLength();
            rpcId.InsertBytes(bytes.Slice(0, ind));
            newHost.InsertBytes(bytes.Slice(ind, newHost.ByteLength()));
        }

        internal static void PingRequestEncode(Span<byte> bytes, PingRequest request)
        {
            var rpcId = new RpcId(RpcId.PingRequestId);
            rpcId.InsertBytes(bytes);
            request.InsertBytes(bytes.Slice(rpcId.ByteLength()));
        }

        internal static RpcId ServerMessageDecode(ReadOnlySpan<byte> bytes, out ConnectionRequest connectRequest)
        {
            RpcId result = RpcId.None;
            result.FromBytes(bytes);
            connectRequest = new ConnectionRequest();
            switch (result.Id)
            {
                case RpcId.ConnectionRequestId:
                    connectRequest.FromBytes(bytes.Slice(result.ByteLength()));
                    break;
            }
            return result;
        }

        internal static bool TryClientMessageDecode(ReadOnlySpan<byte> bytes, out RpcId rpcId)
        {
            rpcId = new RpcId(bytes);
            switch (rpcId.Id)
            {
                case RpcId.ClientConnectedId:
                case RpcId.LocalClientConnectedId:
                case RpcId.ClientDisconnectedId:
                case RpcId.HostMigrationId:
                    return true;
            }
            return false;
        }

        internal static bool TryPingRequestDecode(ReadOnlySpan<byte> bytes, out PingRequest request)
        {
            var rpcId = new RpcId(bytes);
            request = null;
            if (rpcId.Id != RpcId.PingRequestId)
                return false;
            request = new PingRequest(bytes.Slice(rpcId.ByteLength()));
            return true;
        }

        // * Simulation

        internal static int TickMessageLength => RpcId.MaxByteLength + ClientId.MaxByteLength + ClientId.MaxByteLength + Tick.MaxByteLength + 8;

        internal static void EncodeNextTick(Span<byte> bytes, ClientId source, ClientId callee, Tick nextTick, long timestamp = 0)
        {
            var rpcId = new RpcId(RpcId.NextTickId);
            rpcId.InsertBytes(bytes);
            source.InsertBytes(bytes.Slice(RpcId.MaxByteLength));
            callee.InsertBytes(bytes.Slice(RpcId.MaxByteLength + ClientId.MaxByteLength));
            nextTick.InsertBytes(bytes.Slice(RpcId.MaxByteLength + ClientId.MaxByteLength + ClientId.MaxByteLength));
            InsertBytes(bytes.Slice(Tick.MaxByteLength + RpcId.MaxByteLength + ClientId.MaxByteLength + ClientId.MaxByteLength),
                timestamp == 0 ? Timestamp.Now : timestamp);
        }

        internal static void EncodeCurTick(Span<byte> bytes, ClientId source, ClientId callee, Tick curTick, long timestamp = 0)
        {
            var rpcId = new RpcId(RpcId.CurTickId);
            rpcId.InsertBytes(bytes);
            source.InsertBytes(bytes.Slice(RpcId.MaxByteLength));
            callee.InsertBytes(bytes.Slice(RpcId.MaxByteLength + ClientId.MaxByteLength));
            curTick.InsertBytes(bytes.Slice(RpcId.MaxByteLength + ClientId.MaxByteLength + ClientId.MaxByteLength));
            InsertBytes(bytes.Slice(Tick.MaxByteLength + RpcId.MaxByteLength + ClientId.MaxByteLength + ClientId.MaxByteLength),
                timestamp == 0 ? Timestamp.Now : timestamp);
        }

        internal static void EncodeEndTick(Span<byte> bytes, ClientId source, ClientId callee, Tick prevTick, long timestamp = 0)
        {
            var rpcId = new RpcId(RpcId.EndTickId);
            rpcId.InsertBytes(bytes);
            source.InsertBytes(bytes.Slice(RpcId.MaxByteLength));
            callee.InsertBytes(bytes.Slice(RpcId.MaxByteLength + ClientId.MaxByteLength));
            prevTick.InsertBytes(bytes.Slice(RpcId.MaxByteLength + ClientId.MaxByteLength + ClientId.MaxByteLength));
            InsertBytes(bytes.Slice(Tick.MaxByteLength + RpcId.MaxByteLength + ClientId.MaxByteLength + ClientId.MaxByteLength),
                timestamp == 0 ? Timestamp.Now : timestamp);
        }

        internal static bool TryDecodeTickMessage(ReadOnlySpan<byte> bytes, out RpcId rpc, out ClientId source, out ClientId callee, out Tick tick, out long timestamp)
        {
            rpc = new RpcId(bytes);
            switch (rpc.Id)
            {
                case RpcId.NextTickId:
                case RpcId.CurTickId:
                case RpcId.EndTickId:
                    source = new ClientId(bytes.Slice(rpc.ByteLength()));
                    callee = new ClientId(bytes.Slice(rpc.ByteLength(), source.ByteLength()));
                    tick = new Tick(bytes.Slice(rpc.ByteLength() + source.ByteLength() + callee.ByteLength()));
                    timestamp = DecodeInt64(bytes.Slice(rpc.ByteLength() + source.ByteLength() + callee.ByteLength() + tick.ByteLength()));
                    return true;

                default:
                    source = ClientId.None;
                    callee = ClientId.None;
                    tick = new Tick(0);
                    timestamp = 0;
                    return false;
            }
        }

        internal static void DecodeClients(ReadOnlySpan<byte> bytes, out ClientId caller, out ClientId callee)
        {
            caller = new ClientId(bytes);
            callee = new ClientId(bytes.Slice(ClientId.MaxByteLength));
        }

        // * To Strings

        internal static string ToString(ReadOnlySpan<byte> bytes, int rowCount = -1)
        {
            var str = "";
            for (int i = 0; i < bytes.Length; i++)
            {
                str += bytes[i].ToString("X2");
                if (i < bytes.Length - 1)
                    str += '-';
                if (rowCount > 0 && i % rowCount == 0 && i != 0)
                    str += '\n';
            }
            return str;
        }

        internal static void ToString(ReadOnlySpan<byte> bytes, StringBuilder str, int rowCount = -1)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                str.Append(bytes[i].ToString("X2"));
                if (i < bytes.Length - 1)
                    str.Append('-');
                if (rowCount > 0 && i % rowCount == 0 && i != 0)
                    str.Append('\n');
            }
        }
    }
}