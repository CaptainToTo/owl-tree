using System.Reflection;

namespace OwlTree
{
    public class RpcProtocol
    {

        internal const byte CLIENT_CONNECTED_MESSAGE_ID       = 0;
        internal const byte LOCAL_CLIENT_CONNECTED_MESSAGE_ID = 1;
        internal const byte CLIENT_DISCONNECTED_MESSAGE_ID    = 2;
        internal const byte NETWORK_OBJECT_NEW                = 3;
        internal const byte NETWORK_OBJECT_DESTROY            = 4;

        private static byte _curId = 5;

        public RpcProtocol(Type[] paramTypes)
        {
            Id = _curId;
            _curId++;
            ParamTypes = paramTypes;
        }

        public byte Id { get; private set; }


        public Type[] ParamTypes { get; private set; }


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
                t == typeof(char) ||
                t == typeof(string)
            )
            {
                return true;
            }
            else
            {
                var encodableTypes = t.GetInterfaces();
                foreach (var a in encodableTypes)
                {
                    if (a.IsGenericType)
                    {
                        if (a.GetGenericTypeDefinition() == typeof(IEncodable<>))
                            return true;
                    }
                }
                return false;
            }
        }
    }
}