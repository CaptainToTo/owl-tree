using System;

namespace OwlTree
{
    public static partial class Encoder
    {
        /// <summary>
        /// The constant byte count of header info for user made RPCs.
        /// </summary>
        internal static int RpcHeaderLength => ClientId.MaxByteLength + ClientId.MaxByteLength + RpcId.MaxByteLength + NetworkId.MaxByteLength;

        /// <summary>
        /// Gets the expected byte length of a full RPC encoding, given an array of the 
        /// RPC arguments. To get the length of just the arguments, use <c>GetExpectedLength()</c>
        /// If any of the arguments are not encodable, returns -1.
        /// </summary>
        internal static int GetExpectedRpcLength(object[] args, int callerInd = -1, int calleeInd = -1)
        {
            var len = GetExpectedArgsLength(args, callerInd, calleeInd);
            if (len == -1)
                return -1;
            return len + RpcHeaderLength;
        }

        /// <summary>
        /// Gets the expected byte length of all encodable arguments provided in the array.
        /// This only finds the length of the given arguments. To get the length of a full RPC
        /// encoding, use <c>GetExpectedRpcLength()</c>.
        /// If any of the arguments are not encodable, returns -1.
        /// </summary>
        internal static int GetExpectedArgsLength(object[] args, int callerInd = -1, int calleeInd = -1)
        {
            if (args == null)
                return 0;

            int sum = 0;
            for (int i = 0; i < args.Length; i++)
            {
                if (i == callerInd || i == calleeInd) continue;
                var len = GetByteLength(args[i]);
                if (len == -1)
                    return -1;
                sum += len;
            }
            return sum;
        }

        internal static void EncodeRpcHeader(Span<byte> bytes, RpcId id, ClientId caller, ClientId callee, NetworkId source)
        {
            int start = 0;
            int end = id.ByteLength();
            id.InsertBytes(bytes.Slice(start, end - start));
            start = end;
            end += caller.ByteLength();
            caller.InsertBytes(bytes.Slice(start, end - start));
            start = end;
            end += callee.ByteLength();
            callee.InsertBytes(bytes.Slice(start, end - start));
            start = end;
            end += source.ByteLength();
            source.InsertBytes(bytes.Slice(start, end - start));
        }

        /// <summary>
        /// Creates a copy of the given byte array, and replaces the callee bytes with the given callee id
        /// in the copy.
        /// </summary>
        internal static byte[] ChangeRpcCallee(Span<byte> bytes, ClientId callee)
        {
            var newBytes = bytes.ToArray();
            callee.InsertBytes(newBytes.AsSpan(RpcId.MaxByteLength + ClientId.MaxByteLength, callee.ByteLength()));
            return newBytes;
        }

        /// <summary>
        /// Encodes an RPC call into the given span of bytes. This span must have enough space, which can be verified
        /// using <c>GetExpectedRpcLength()</c>.
        /// </summary>
        internal static void EncodeRpc(Span<byte> bytes, RpcId id, ClientId caller, ClientId callee, NetworkId source, object[] args, int calleeInd, int callerInd)
        {
            int start = 0;
            int end = id.ByteLength();
            id.InsertBytes(bytes.Slice(start, end - start));
            start = end;
            end += caller.ByteLength();
            caller.InsertBytes(bytes.Slice(start, end - start));
            start = end;
            end += callee.ByteLength();
            callee.InsertBytes(bytes.Slice(start, end - start));
            start = end;
            end += source.ByteLength();
            source.InsertBytes(bytes.Slice(start, end - start));

            if (args == null)
                return;

            for (int i = 0; i < args.Length; i++)
            {
                if (i == callerInd || i == calleeInd) continue;
                start = end;
                end += GetByteLength(args[i]);
                InsertBytes(bytes.Slice(start, end - start), args[i]);
            }

            return;
        }

        /// <summary>
        /// Decodes all header info from an rpc encoding.
        /// </summary>
        internal static void DecodeRpcHeader(ReadOnlySpan<byte> bytes, out RpcId rpc, out ClientId caller, out ClientId callee, out NetworkId target)
        {
            if (RpcHeaderLength > bytes.Length)
            {
                rpc = RpcId.None;
                caller = ClientId.None;
                callee = ClientId.None;
                target = NetworkId.None;
                return;
            }

            int ind = 0;
            rpc = new RpcId(bytes);
            ind += rpc.ByteLength();
            caller = new ClientId(bytes.Slice(ind));
            ind += caller.ByteLength();
            callee = new ClientId(bytes.Slice(ind));
            ind += callee.ByteLength();

            if (rpc.Id >= RpcId.FirstRpcId)
                target = new NetworkId(bytes.Slice(ind));
            else
                target = NetworkId.None;
        }

        /// <summary>
        /// Decodes an RPC argument encoding using the given span and parameter types. Returns the decoded arguments
        /// as an array of objects. Uses provided caller and callee arguments to find and replace any RpcCaller or RpcCallee parameters.
        /// </summary>
        internal static object[] DecodeRpcArgs(ReadOnlySpan<byte> bytes, ClientId caller, ClientId callee, Type[] paramTypes, int callerInd, int calleeInd)
        {
            object[] args = new object[paramTypes.Length];

            int ind = 0;
            for (int i = 0; i < paramTypes.Length; i++)
            {
                if (i == callerInd)
                {
                    args[i] = caller;
                }
                else if (i == calleeInd)
                {
                    args[i] = callee;
                }
                else
                {
                    args[i] = DecodeObject(bytes.Slice(ind), paramTypes[i], out var len);
                    ind += len;
                }
            }

            return args;
        }
    }
}