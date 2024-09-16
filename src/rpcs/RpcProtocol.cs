using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace OwlTree
{
    public class RpcProtocol
    {

        public RpcProtocol(Type networkObjectType, MethodInfo method, Type[] paramTypes)
        {
            Id = new RpcId();
            ParamTypes = paramTypes;
            Method = method;
            NetworkObjectType = networkObjectType;
        }

        public RpcId Id { get; private set; }

        public Type NetworkObjectType { get; private set; }

        public MethodInfo Method { get; private set; }

        public Type[] ParamTypes { get; private set; }

        public override string ToString()
        {
            string title = Method.Name + " <RpcId: " + Id + ">:\n";
            string encoding = "  Bytes: [ RpcId:" + RpcId.MaxLength() + "b ][ NetId:" + NetworkId.MaxLength() + "b ]";
            string parameters = "";
            int maxSize = RpcId.MaxLength() + NetworkId.MaxLength();
            var paramList = Method.GetParameters();
            for (int i = 0; i < paramList.Length; i++)
            {
                var param = paramList[i];
                int size = GetMaxLength(param.ParameterType);
                maxSize += size;
                encoding += "[ " + (i + 1) + ":" + size + "b ]";
                parameters += "    " + (i + 1) + ": " + param.ParameterType.ToString() + " " + param.Name;
                if (param.CustomAttributes.Any(a => a.AttributeType == typeof(RpcCalleeAttribute)))
                {
                    parameters += " [Callee]";
                }
                else if (param.CustomAttributes.Any(a => a.AttributeType == typeof(RpcCallerAttribute)))
                {
                    parameters += " [Caller]";
                }
                parameters += "\n";
            }
            return title + encoding + " = " + maxSize + " max bytes\n" + parameters;
        }

        public byte[] Encode(NetworkId source, object[]? args)
        {
            if ((args == null && ParamTypes.Length > 0) || (args != null && args.Length != ParamTypes.Length))
                throw new ArgumentException("args array must have the same number of elements as the expected method parameters.");

            byte[] bytes = new byte[Id.ExpectedLength() + source.ExpectedLength() + ExpectedLength(args)];

            int ind = 0;
            Id.InsertBytes(ref bytes, ref ind);
            source.InsertBytes(ref bytes, ref ind);

            if (args == null)
                return bytes;

            for (int i = 0; i < ParamTypes.Length; i++)
            {
                if (ParamTypes[i] != args[i].GetType())
                    throw new ArgumentException("args must have the same types as the expected method parameters, in the correct order.");
                InsertBytes(bytes, args[i], ref ind);
            }

            return bytes;
        }

        public void Invoke(NetworkObject target, object[]? args)
        {
            if (target == null && !Method.IsStatic)
                throw new ArgumentException("Target can only be null if the RPC is a static method.");

            if (target != null && target.GetType() != NetworkObjectType)
                throw new ArgumentException("Target must match this RPC's type");
            
            Method.Invoke(target, args);
        }

        public object[] Decode(ClientId source, byte[] bytes, ref int ind, out NetworkId target)
        {
            if (new RpcId(bytes, ind) != Id)
                throw new ArgumentException("Given bytes must match this protocol. RPC id did not match.");

            ind += Id.ExpectedLength();

            target = (NetworkId)NetworkId.FromBytesAt(bytes, ref ind);

            object[] args = new object[ParamTypes.Length];

            var paramList = Method.GetParameters();
            for (int i = 0; i < ParamTypes.Length; i++)
            {
                if (paramList[i].CustomAttributes.Any(a => a.AttributeType == typeof(RpcCallerAttribute)))
                {
                    args[i] = source;
                }
                else
                {
                    args[i] = DecodeObject(bytes, ref ind, ParamTypes[i]);
                }
            }

            return args;
        }

        private static object DecodeObject(byte[] bytes, ref int ind, Type t)
        {
            if (t == typeof(string))
            {
                var length = bytes[ind];
                var str = Encoding.UTF8.GetString(bytes, ind + 1, length);
                ind += length + 1;
                return str;
            }

            object result = Activator.CreateInstance(t)!;
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
                        result = t.GetMethod("FromBytesAt")!.Invoke(null, paramList)!;
                        ind = (int)paramList[1];
                        break;
                    }
                }
            }

            return result;
        }

        private static void InsertBytes(byte[] bytes, object arg, ref int ind)
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

        public int ExpectedLength(object[]? args)
        {
            if (args == null)
                return 0;

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

        private static int GetMaxLength(Type t)
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
                return 256;
            }
            else
            {
                var encodable = typeof(IEncodable);
                var encodableTypes = t.GetInterfaces();
                foreach (var a in encodableTypes)
                {
                    if (a == encodable)
                    {
                        var method = t.GetMethod("MaxLength", BindingFlags.Static | BindingFlags.Public)!;
                        return (int)method.Invoke(null, null)!;
                    }
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
                if (!t.IsValueType)
                    return false;

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