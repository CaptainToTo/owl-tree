using System.Reflection;
using System.Text;

namespace OwlTree
{
    public class RpcProtocol
    {

        internal const byte CLIENT_CONNECTED_MESSAGE_ID       = 0;
        internal const byte LOCAL_CLIENT_CONNECTED_MESSAGE_ID = 1;
        internal const byte CLIENT_DISCONNECTED_MESSAGE_ID    = 2;
        internal const byte NETWORK_OBJECT_NEW                = 3;
        internal const byte NETWORK_OBJECT_DESTROY            = 4;
        internal const byte FIRST_RPC_ID                      = 5;

        private static byte _curId = FIRST_RPC_ID;

        public RpcProtocol(MethodInfo method, Type[] paramTypes)
        {
            Id = _curId;
            _curId++;
            ParamTypes = paramTypes;
            Method = method;
        }

        public byte Id { get; private set; }

        public MethodInfo Method { get; private set; }

        public Type[] ParamTypes { get; private set; }

        public byte[] Encode(object[] args)
        {
            if (args.Length != ParamTypes.Length)
                throw new ArgumentException("args array must have the same number of elements as the expected method parameters.");

            byte[] bytes = new byte[1 + ExpectedLength(args)];

            bytes[0] = Id;

            int i = 1;
            foreach (var arg in args)
            {
                if (ParamTypes[i] != args[i].GetType())
                    throw new ArgumentException("args must have the same types as the expected method parameters, in the correct order.");
                
                InsertBytes(ref bytes, arg, ref i);
            }

            return bytes;
        }

        public object?[] Decode(byte[] bytes, int ind)
        {
            if (bytes[ind] != Id)
                throw new ArgumentException("Given bytes must match this protocol. RPC id did not match.");

            ind += 1;

            object?[] args = new object[ParamTypes.Length];

            for (int i = 0; i < ParamTypes.Length; i++)
            {
                args[i] = DecodeObject(bytes, ref ind, ParamTypes[i]);
            }

            return args;
        }

        private static object? DecodeObject(byte[] bytes, ref int ind, Type t)
        {
            object? result = null;
            if (t == typeof(int))
            {
                result = BitConverter.ToInt32(bytes.AsSpan(ind));
                ind += 4;
            }
            else if (t == typeof(uint))
            {
                result = BitConverter.ToUInt32(bytes.AsSpan(ind));
                ind += 4;
            }
            else if (t == typeof(float))
            {
                result = BitConverter.ToSingle(bytes.AsSpan(ind));
                ind += 4;
            }
            else if (t == typeof(double))
            {
                result = BitConverter.ToDouble(bytes.AsSpan(ind));
                ind += 8;
            }
            else if (t == typeof(long))
            {
                result = BitConverter.ToInt64(bytes.AsSpan(ind));
                ind += 8;
            }
            else if (t == typeof(ushort))
            {
                result = BitConverter.ToUInt16(bytes.AsSpan(ind));
                ind += 2;
            }
            else if (t == typeof(byte))
            {
                result = bytes[ind];
                ind += 1;
            }
            else if (t == typeof(bool))
            {
                result = bytes[ind] == 1;
                ind += 1;
            }
            else if (t == typeof(string))
            {
                var length = bytes[ind];
                result = Encoding.UTF8.GetString(bytes, ind + 1, length);
                ind += length + 1;
            }
            else
            {
                var encodable = typeof(IEncodable);
                var encodableTypes = t.GetInterfaces();
                foreach (var a in encodableTypes)
                {
                    if (a == encodable)
                    {
                        int curInd = ind;
                        object[] paramList = [bytes, curInd];
                        result = t.GetMethod("FromBytes")?.Invoke(null, paramList);
                        ind = (int)paramList[1];
                        break;
                    }
                }
            }

            return result;
        }

        private static void InsertBytes(ref byte[] bytes, object arg, ref int ind)
        {
            var t = arg.GetType();
            if (t == typeof(int))
            {
                BitConverter.TryWriteBytes(bytes.AsSpan(ind), (int)arg);
                ind += 4;
            }
            else if (t == typeof(uint))
            {
                BitConverter.TryWriteBytes(bytes.AsSpan(ind), (uint)arg);
                ind += 4;
            }
            else if (t == typeof(float))
            {
                BitConverter.TryWriteBytes(bytes.AsSpan(ind), (float)arg);
                ind += 4;
            }
            else if (t == typeof(double))
            {
                BitConverter.TryWriteBytes(bytes.AsSpan(ind), (double)arg);
                ind += 8;
            }
            else if (t == typeof(long))
            {
                BitConverter.TryWriteBytes(bytes.AsSpan(ind), (long)arg);
                ind += 8;
            }
            else if (t == typeof(ushort))
            {
                BitConverter.TryWriteBytes(bytes.AsSpan(ind), (ushort)arg);
                ind += 2;
            }
            else if (t == typeof(byte))
            {
                bytes[ind] = (byte)arg;
                ind += 1;
            }
            else if (t == typeof(bool))
            {
                bytes[ind] = (byte)(((bool)arg) ? 1 : 0);
                ind += 1;
            }
            else if (t == typeof(string))
            {
                var encoding = Encoding.UTF8.GetBytes((string)arg);
                if (encoding.Length > 255)
                    throw new InvalidOperationException("strings cannot require more than 255 bytes to encode.");
                bytes[ind] = (byte)encoding.Length;
                ind += 1;
                for (int i = 0; i < encoding.Length; i++)
                    bytes[ind + i] = encoding[i];
                ind += encoding.Length;
            }
            else
            {
                var encodable = typeof(IEncodable);
                var encodableTypes = t.GetInterfaces();
                foreach (var a in encodableTypes)
                {
                    if (a == encodable)
                    {
                        ((IEncodable)arg).InsertBytes(ref bytes, ref ind);
                    }
                }
            }
        }

        public int ExpectedLength(object[] args)
        {
            if (args.Length != ParamTypes.Length)
                throw new ArgumentException("args array must have the same number of elements as the expected method parameters.");
            int sum = 1; // 1 for the rpc id
            for (int i = 0; i < args.Length; i++)
            {
                if (ParamTypes[i] != args[i].GetType())
                    throw new ArgumentException("args must have the same types as the expected method parameters, in the correct order.");
                sum += GetExpectedLength(args[i]);
            }
            return sum;
        }

        private static int GetExpectedLength(object arg)
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
                t == typeof(long)
            )
            {
                return 8;
            }
            else if (t == typeof(ushort))
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
                var encodableTypes = t.GetInterfaces();
                foreach (var a in encodableTypes)
                {
                    if (a == encodable)
                        return ((IEncodable)arg).ExpectedLength();
                }
            }
            return -1;
        }

        public static bool IsEncodableParam(ParameterInfo arg)
        {
            var t = arg.ParameterType;
            if (
                t == typeof(int) ||
                t == typeof(uint) ||
                t == typeof(float) ||
                t == typeof(double) ||
                t == typeof(long) ||
                t == typeof(ushort) ||
                t == typeof(byte) ||
                t == typeof(string) ||
                t == typeof(bool)
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