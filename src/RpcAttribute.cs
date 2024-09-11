
using System.Reflection;

namespace OwlTree
{
    /// <summary>
    /// Who is allowed to call the RPC.
    /// </summary>
    public enum RpcCaller
    {
        /// <summary>
        /// Only the server is allowed to call this RPC, meaning it will be executed on clients.
        /// </summary>
        Server,
        /// <summary>
        /// Only clients are allowed to call this RPC, meaning it will be executed on the server.
        /// </summary>
        Client,
        /// <summary>
        /// Both clients and the server are allowed to call this RPC.
        /// </summary>
        Any
    }

    /// <summary>
    /// Tag a method as an RPC. All parameters must be encodable as a byte array, and the return type must be void.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class RpcAttribute : Attribute
    {
        public RpcCaller caller = RpcCaller.Any;

        /// <summary>
        /// Tag a method as an RPC. All parameters must be encodable as a byte array, and the return type must be void.
        /// </summary>
        public RpcAttribute(RpcCaller caller)
        {
            this.caller = caller;
        }

        struct RpcProtocol
        {
            public byte id;
            public Type[] paramTypes;
        }

        // private static 

        public static void GenerateRpcProtocols()
        {
            Type t = typeof(NetworkObject);
            Type encodable = typeof(IEncodable<>);

            var rpcs = t.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public)
                        .Where(m => m.GetCustomAttributes(typeof(RpcAttribute), false).Length > 0)
                        .ToArray();
            
            foreach (var rpc in rpcs)
            {
                var args = rpc.GetParameters();
                Console.WriteLine(rpc.Name + ":");
                foreach (var arg in args)
                {
                    Console.WriteLine("    " + arg.ParameterType.FullName + " " + arg.Name);
                    var encodableTypes = arg.ParameterType.GetInterfaces();
                    bool foundEncodable = false;
                    foreach (var a in encodableTypes)
                    {
                        if (a.IsGenericType)
                        {
                            if (a.GetGenericTypeDefinition() == encodable)
                                foundEncodable = true;
                                break;
                        }
                    }
                    if (encodableTypes == null || encodableTypes.Length == 0 || !foundEncodable)
                    {
                        throw new ArgumentException("All arguments must be convertable to a byte array.");
                    }
                }
            }
        }
    }
}