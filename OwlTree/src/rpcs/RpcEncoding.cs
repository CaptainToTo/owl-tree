

using System;
using System.Linq;
using System.Text;

namespace OwlTree
{
    // TODO: remove reflection
    /// <summary>
    /// Helper class that contains methods for encoding and decoding Rpcs, and IEncodable objects.
    /// </summary>
    public static class RpcEncoding
    {
        /// <summary>
        /// Encodes an RPC call into the given span of bytes. This span must have enough space, which can be verified
        /// using <c>GetExpectedRpcLength()</c>.
        /// </summary>
        public static void EncodeRpc(Span<byte> bytes, RpcId id, NetworkId source, object[] args)
        {
            int start = 0;
            int end = id.ByteLength();
            id.InsertBytes(bytes.Slice(start, end - start));
            start = end;
            end += source.ByteLength();
            source.InsertBytes(bytes.Slice(start, end - start));

            if (args == null)
                return;

            for (int i = 0; i < args.Length; i++)
            {
                start = end;
                end += GetExpectedLength(args[i]);
                InsertBytes(bytes.Slice(start, end - start), args[i]);
            }

            return;
        }

        /// <summary>
        /// Decodes an RPC encoding using the given span and parameter types. Returns the decoded arguments
        /// as an array of objects, and outputs the decoded RpcId, and the NetworkId of the NetworkObject the 
        /// RPC was called from.
        /// </summary>
        public static object[] DecodeRpc(ClientId source, ReadOnlySpan<byte> bytes, Type[] paramTypes, out RpcId id, out NetworkId target)
        {
            id = new RpcId(bytes);
            int ind = id.ByteLength();

            target = new NetworkId(bytes.Slice(ind, NetworkId.MaxLength()));
            ind += target.ByteLength();

            object[] args = new object[paramTypes.Length];

            for (int i = 0; i < paramTypes.Length; i++)
            {
                if (paramTypes[i].CustomAttributes.Any(a => a.AttributeType == typeof(RpcCallerAttribute)))
                {
                    args[i] = source;
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
        internal static object DecodeObject(ReadOnlySpan<byte> bytes, Type t, out int len)
        {
            if (t == typeof(string))
            {
                var length = bytes[0];
                var str = Encoding.UTF8.GetString(bytes.ToArray(), 1, length);
                len = length + 1;
                return str;
            }

            object result = Activator.CreateInstance(t);
            if (t == typeof(int))
            {
                result = BitConverter.ToInt32(bytes);
                len = 4;
            }
            else if (t == typeof(uint))
            {
                result = BitConverter.ToUInt32(bytes);
                len = 4;
            }
            else if (t == typeof(float))
            {
                result = BitConverter.ToSingle(bytes);
                len = 4;
            }
            else if (t == typeof(double))
            {
                result = BitConverter.ToDouble(bytes);
                len = 8;
            }
            else if (t == typeof(long))
            {
                result = BitConverter.ToInt64(bytes);
                len = 8;
            }
            else if (t == typeof(ulong))
            {
                result = BitConverter.ToUInt64(bytes);
                len = 8;
            }
            else if (t == typeof(ushort))
            {
                result = BitConverter.ToUInt16(bytes);
                len = 2;
            }
            else if (t == typeof(short))
            {
                result = BitConverter.ToInt16(bytes);
                len = 2;
            }
            else if (t == typeof(byte))
            {
                result = bytes[0];
                len = 1;
            }
            else if (t == typeof(bool))
            {
                result = bytes[0] == 1;
                len = 1;
            }
            else
            {
                var encodable = typeof(IEncodable);
                var variableLen = typeof(IVariableLength);
                var encodableTypes = t.GetInterfaces();
                bool isEncodable = false;
                bool isVariable = false;
                foreach (var a in encodableTypes)
                {
                    if (a == encodable)
                    {
                        isEncodable = true;
                    }
                    else if (a == variableLen)
                    {
                        isVariable = true;
                    }
                }

                if (isEncodable)
                {
                    len = isVariable ? IVariableLength.GetLength(bytes) : ((IEncodable)result).ByteLength();
                    ((IEncodable)result).FromBytes(bytes.Slice(isVariable ? IVariableLength.LENGTH_ENCODING : 0, len));
                    len += isVariable ? IVariableLength.LENGTH_ENCODING : 0;
                }
                else
                {
                    len = -1;
                }
            }

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
                var encodable = typeof(IEncodable);
                var variableLen = typeof(IVariableLength);
                var encodableTypes = t.GetInterfaces();
                bool isEncodable = false;
                bool isVariable = false;
                foreach (var a in encodableTypes)
                {
                    if (a == encodable)
                    {
                        isEncodable = true;
                    }
                    if (a == variableLen)
                    {
                        isVariable = true;
                    }
                }

                if (isEncodable)
                {
                    if (isVariable)
                    {
                        IVariableLength.InsertLength(bytes, ((IEncodable)arg).ByteLength());
                        bytes = bytes.Slice(IVariableLength.LENGTH_ENCODING);
                    }
                    ((IEncodable)arg).InsertBytes(bytes);
                }
            }
        }

        /// <summary>
        /// Gets the expected byte length of a full RPC encoding, given an array of the 
        /// RPC arguments. To get the length of just the arguments, use <c>GetExpectedLength()</c>
        /// If any of the arguments are not encodable, returns -1.
        /// </summary>
        public static int GetExpectedRpcLength(object[] args)
        {
            var len = GetExpectedLength(args);
            if (len == -1)
                return -1;
            return len + RpcId.MaxLength() + NetworkId.MaxLength();
        }

        /// <summary>
        /// Gets the expected byte length of all encodable arguments provided in the array.
        /// This only finds the length of the given arguments. To get the length of a full RPC
        /// encoding, use <c>GetExpectedRpcLength()</c>.
        /// If any of the arguments are not encodable, returns -1.
        /// </summary>
        public static int GetExpectedLength(object[] args)
        {
            if (args == null)
                return 0;

            int sum = 0;
            foreach (var arg in args)
            {
                var len = GetExpectedLength(arg);
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
            else
            {
                var encodable = typeof(IEncodable);
                var variableLen = typeof(IVariableLength);
                var encodableTypes = t.GetInterfaces();
                var len = -1;
                foreach (var a in encodableTypes)
                {
                    if (a == encodable)
                        len = ((IEncodable)arg).ByteLength();
                    else if (a == variableLen)
                        return ((IEncodable)arg).ByteLength() + IVariableLength.LENGTH_ENCODING;
                }
                return len;
            }
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
        public static bool IsEncodable(Type t)
        {
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
                var encodable = typeof(IEncodable);
                var encodableTypes = t.GetInterfaces();
                foreach (var a in encodableTypes)
                {
                    if (a == encodable)
                        return true;
                }
                return false;
            }
        }
    }
}