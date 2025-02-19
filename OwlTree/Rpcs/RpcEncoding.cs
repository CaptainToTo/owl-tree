using System;
using System.Text;

namespace OwlTree
{
    // TODO: remove reflection
    /// <summary>
    /// Helper class that contains methods for encoding and decoding Rpcs, and IEncodable objects.
    /// </summary>
    public static class RpcEncoding
    {
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
                end += GetExpectedLength(args[i]);
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

        /// <summary>
        /// Decodes an object of the given type from the given bytes. Returns the decoded object,
        /// and the number of bytes that were read. If the given type isn't encodable, then len will be set to -1,
        /// and an empty object will be returned.
        /// </summary>
        public static object DecodeObject(ReadOnlySpan<byte> bytes, Type t, out int len)
        {
            if (t == typeof(string))
                return DecodeString(bytes, out len);

            else if (t == typeof(int))
                return DecodeInt32(bytes, out len);

            else if (t == typeof(uint))
                return DecodeUInt32(bytes, out len);

            else if (t == typeof(float))
                return DecodeFloat(bytes, out len);

            else if (t == typeof(double))
                return DecodeDouble(bytes, out len);

            else if (t == typeof(long))
                return DecodeInt64(bytes, out len);

            else if (t == typeof(ulong))
                return DecodeUInt64(bytes, out len);

            else if (t == typeof(ushort))
                return DecodeUInt16(bytes, out len);

            else if (t == typeof(short))
                return DecodeInt16(bytes, out len);

            else if (t == typeof(bool))
                return DecodeBool(bytes, out len);

            else if (t == typeof(byte))
            {
                len = 1;
                return bytes[0];
            }
            else
            {
                var result = Activator.CreateInstance(t);
                bool isEncodable = result is IEncodable;
                bool isVariable = result is IVariableLength;

                if (isEncodable)
                {
                    len = isVariable ? IVariableLength.GetLength(bytes) : ((IEncodable)result).ByteLength();
                    ((IEncodable)result).FromBytes(bytes.Slice(isVariable ? IVariableLength.LengthEncoding : 0, len));
                    len += isVariable ? IVariableLength.LengthEncoding : 0;
                    return result;
                }
                else
                {
                    len = -1;
                    return null;
                }
            }

        }

        public static int DecodeInt32(ReadOnlySpan<byte> bytes, out int len)
        {
            len = 4;
            return BitConverter.ToInt32(bytes);
        }

        public static uint DecodeUInt32(ReadOnlySpan<byte> bytes, out int len)
        {
            len = 4;
            return BitConverter.ToUInt32(bytes);
        }

        public static float DecodeFloat(ReadOnlySpan<byte> bytes, out int len)
        {
            len = 4;
            return BitConverter.ToSingle(bytes);
        }

        public static long DecodeInt64(ReadOnlySpan<byte> bytes, out int len)
        {
            len = 8;
            return BitConverter.ToInt64(bytes);
        }

        public static ulong DecodeUInt64(ReadOnlySpan<byte> bytes, out int len)
        {
            len = 8;
            return BitConverter.ToUInt64(bytes);
        }

        public static double DecodeDouble(ReadOnlySpan<byte> bytes, out int len)
        {
            len = 8;
            return BitConverter.ToDouble(bytes);
        }

        public static short DecodeInt16(ReadOnlySpan<byte> bytes, out int len)
        {
            len = 2;
            return BitConverter.ToInt16(bytes);
        }

        public static ushort DecodeUInt16(ReadOnlySpan<byte> bytes, out int len)
        {
            len = 2;
            return BitConverter.ToUInt16(bytes);
        }

        public static bool DecodeBool(ReadOnlySpan<byte> bytes, out int len)
        {
            len = 1;
            return bytes[0] == 1;
        }

        public static string DecodeString(ReadOnlySpan<byte> bytes, out int len)
        {
            var length = bytes[0];
            var str = Encoding.UTF8.GetString(bytes.ToArray(), 1, length);
            len = length + 1;
            return str;
        }

        public static T DecodeEncodable<T>(ReadOnlySpan<byte> bytes, out int len) where T : IEncodable, new()
        {
            T result = new T();
            var isVariable = result is IVariableLength;

            len = isVariable ? IVariableLength.GetLength(bytes) : result.ByteLength();
            result.FromBytes(bytes.Slice(isVariable ? IVariableLength.LengthEncoding : 0, len));
            len += isVariable ? IVariableLength.LengthEncoding : 0;

            return result;
        }

        /// <summary>
        /// Encodes the given encodable object into the given span of bytes.
        /// Verify the object is encodable with <c>IsEncodableParam()</c>.
        /// Verify the span provides enough bytes by comparing it's length to
        /// the result of <c>GetExpectedLength()</c>
        /// </summary>
        public static void InsertBytes(Span<byte> bytes, object arg)
        {
            var t = arg.GetType();
            if (t == typeof(int))
            {
                BitConverter.TryWriteBytes(bytes, (int)arg);
            }
            else if (t == typeof(uint))
            {
                BitConverter.TryWriteBytes(bytes, (uint)arg);
            }
            else if (t == typeof(float))
            {
                BitConverter.TryWriteBytes(bytes, (float)arg);
            }
            else if (t == typeof(double))
            {
                BitConverter.TryWriteBytes(bytes, (double)arg);
            }
            else if (t == typeof(long))
            {
                BitConverter.TryWriteBytes(bytes, (long)arg);
            }
            else if (t == typeof(ulong))
            {
                BitConverter.TryWriteBytes(bytes, (ulong)arg);
            }
            else if (t == typeof(ushort))
            {
                BitConverter.TryWriteBytes(bytes, (ushort)arg);
            }
            else if (t == typeof(short))
            {
                BitConverter.TryWriteBytes(bytes, (short)arg);
            }
            else if (t == typeof(byte))
            {
                bytes[0] = (byte)arg;
            }
            else if (t == typeof(bool))
            {
                bytes[0] = (byte)(((bool)arg) ? 1 : 0);
            }
            else if (t == typeof(string))
            {
                var encoding = Encoding.UTF8.GetBytes((string)arg);
                if (encoding.Length > 255)
                    throw new InvalidOperationException("strings cannot require more than 255 bytes to encode.");
                bytes[0] = (byte)encoding.Length;
                for (int i = 0; i < encoding.Length; i++)
                    bytes[i + 1] = encoding[i];
            }
            else
            {
                bool isEncodable = arg is IEncodable;
                bool isVariable = arg is IVariableLength;

                if (isEncodable)
                {
                    if (isVariable)
                    {
                        IVariableLength.InsertLength(bytes, ((IEncodable)arg).ByteLength());
                        bytes = bytes.Slice(IVariableLength.LengthEncoding);
                    }
                    ((IEncodable)arg).InsertBytes(bytes);
                }
            }
        }

        public static void InsertBytes(Span<byte> bytes, int arg) => BitConverter.TryWriteBytes(bytes, arg);
        public static void InsertBytes(Span<byte> bytes, uint arg) => BitConverter.TryWriteBytes(bytes, arg);
        public static void InsertBytes(Span<byte> bytes, float arg) => BitConverter.TryWriteBytes(bytes, arg);

        public static void InsertBytes(Span<byte> bytes, long arg) => BitConverter.TryWriteBytes(bytes, arg);
        public static void InsertBytes(Span<byte> bytes, ulong arg) => BitConverter.TryWriteBytes(bytes, arg);
        public static void InsertBytes(Span<byte> bytes, double arg) => BitConverter.TryWriteBytes(bytes, arg);

        public static void InsertBytes(Span<byte> bytes, short arg) => BitConverter.TryWriteBytes(bytes, arg);
        public static void InsertBytes(Span<byte> bytes, ushort arg) => BitConverter.TryWriteBytes(bytes, arg);

        public static void InsertBytes(Span<byte> bytes, byte arg) => bytes[0] = arg;
        public static void InsertBytes(Span<byte> bytes, bool arg) => bytes[0] = (byte)(arg ? 1 : 0);

        public static void InsertBytes(Span<byte> bytes, string arg)
        {
            var encoding = Encoding.UTF8.GetBytes(arg);
            if (encoding.Length > 255)
                throw new InvalidOperationException("strings cannot require more than 255 bytes to encode.");
            bytes[0] = (byte)encoding.Length;
            for (int i = 0; i < encoding.Length; i++)
                bytes[i + 1] = encoding[i];
        }

        public static void InsertBytes(Span<byte> bytes, IEncodable arg)
        {
            if (arg is IVariableLength)
            {
                IVariableLength.InsertLength(bytes, arg.ByteLength());
                bytes = bytes.Slice(IVariableLength.LengthEncoding);
            }
            arg.InsertBytes(bytes);
        }

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
            var len = GetExpectedLength(args, callerInd, calleeInd);
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
        internal static int GetExpectedLength(object[] args, int callerInd = -1, int calleeInd = -1)
        {
            if (args == null)
                return 0;

            int sum = 0;
            for (int i = 0; i < args.Length; i++)
            {
                if (i == callerInd || i == calleeInd) continue;
                var len = GetExpectedLength(args[i]);
                if (len == -1)
                    return -1;
                sum += len;
            }
            return sum;
        }

        /// <summary>
        /// Gets the expected byte length of the given encodable object.
        /// If the object is not encodable, return -1.
        /// </summary>
        public static int GetExpectedLength(object arg)
        {
            var t = arg.GetType();
            if (
                t == typeof(int) ||
                t == typeof(uint) ||
                t == typeof(float)
            )
            {
                return 4;
            }
            else if (
                t == typeof(double) ||
                t == typeof(long) ||
                t == typeof(ulong)
            )
            {
                return 8;
            }
            else if (
                t == typeof(ushort) ||
                t == typeof(short)
            )
            {
                return 2;
            }
            else if (
                t == typeof(byte) ||
                t == typeof(bool)
            )
            {
                return 1;
            }
            else if (t == typeof(string))
            {
                return 1 + Encoding.UTF8.GetByteCount((string)arg);
            }
            else if (arg is IEncodable)
            {
                if (arg is IVariableLength)
                    return ((IEncodable)arg).ByteLength() + IVariableLength.LengthEncoding;
                return ((IEncodable)arg).ByteLength();
            }
            return -1;
        }

        public static int GetExpectedLength(int arg) => 4;
        public static int GetExpectedLength(uint arg) => 4;
        public static int GetExpectedLength(float arg) => 4;

        public static int GetExpectedLength(long arg) => 8;
        public static int GetExpectedLength(ulong arg) => 8;
        public static int GetExpectedLength(double arg) => 8;

        public static int GetExpectedLength(short arg) => 2;
        public static int GetExpectedLength(ushort arg) => 2;

        public static int GetExpectedLength(byte arg) => 1;
        public static int GetExpectedLength(bool arg) => 1;

        public static int GetExpectedLength(string arg) => 1 + Encoding.UTF8.GetByteCount(arg);

        public static int GetExpectedLength(IEncodable arg)
        {
            if (arg is IVariableLength)
                return arg.ByteLength() + IVariableLength.LengthEncoding;
            return arg.ByteLength();
        }

        /// <summary>
        /// Returns the maximum number of bytes the given type of encodable object can take.
        /// If the type is not encodable, returns -1.
        /// </summary>
        public static int GetMaxLength(Type t)
        {
            if (
                t == typeof(int) ||
                t == typeof(uint) ||
                t == typeof(float)
            )
            {
                return 4;
            }
            else if (
                t == typeof(double) ||
                t == typeof(long) ||
                t == typeof(ulong)
            )
            {
                return 8;
            }
            else if (
                t == typeof(ushort) ||
                t == typeof(short)
            )
            {
                return 2;
            }
            else if (
                t == typeof(byte) ||
                t == typeof(bool)
            )
            {
                return 1;
            }
            else if (t == typeof(string))
            {
                return 256;
            }
            else
            {
                var encodable = typeof(IEncodable);
                var variableLen = typeof(IVariableLength);
                var encodableTypes = t.GetInterfaces();
                int len = -1;
                foreach (var a in encodableTypes)
                {
                    if (a == encodable)
                    {
                        IEncodable obj = (IEncodable)Activator.CreateInstance(t);
                        len = obj.ByteLength();
                    }
                    else if (a == variableLen)
                    {
                        IVariableLength obj = (IVariableLength)Activator.CreateInstance(t);
                        return obj.MaxLength();
                    }
                }
                return len;
            }
        }

        /// <summary>
        /// Returns whether or not the given type represents an encodable object,
        /// which can be used an RPC parameter.
        /// </summary>
        public static bool IsEncodable<T>() where T : new()
        {
            var t = typeof(T);
            if (
                t == typeof(int) ||
                t == typeof(uint) ||
                t == typeof(float) ||

                t == typeof(double) ||
                t == typeof(long) ||
                t == typeof(ulong) ||

                t == typeof(ushort) ||
                t == typeof(short) ||

                t == typeof(byte) ||
                t == typeof(bool) ||

                t == typeof(string)
            )
            {
                return true;
            }
            else
            {
                var a = new T();
                return a is IEncodable;
            }
        }

        /// <summary>
        /// Calculates the byte length for an IEncodable, based on the properties
        /// you want to have encoded.
        /// </summary>
        public static int AutoByteLength(params object[] members)
        {
            return GetExpectedLength(members);
        }

        public static void AutoInsertBytes(Span<byte> bytes, params object[] members)
        {
            int ind = 0;
            foreach (var m in members)
            {
                InsertBytes(bytes.Slice(ind), m);
                ind += GetExpectedLength(m);
            }
        }
    }
}