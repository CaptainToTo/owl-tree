
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

        private static Dictionary<MethodInfo, RpcProtocol> _protocolsByMethod = new Dictionary<MethodInfo, RpcProtocol>();
        private static Dictionary<byte, RpcProtocol> _protocolsById = new Dictionary<byte, RpcProtocol>();

        public static void GenerateRpcProtocols()
        {
            Type t = typeof(NetworkObject);
            // Type encodable = typeof(IEncodable);

            var rpcs = t.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public)
                        .Where(m => m.GetCustomAttributes(typeof(RpcAttribute), false).Length > 0)
                        .ToArray();
            
            foreach (var rpc in rpcs)
            {
                if (rpc.ReturnType != typeof(void))
                    throw new InvalidOperationException("RPC return types must be void.");

                var args = rpc.GetParameters();
                Type[] paramTypes = new Type[args.Length];

                Console.WriteLine(rpc.Name + ":");
                for (int i = 0; i < args.Length; i++)
                {
                    var arg = args[i];
                    Console.WriteLine("    " + arg.ParameterType.FullName + " " + arg.Name);

                    if (!RpcProtocol.IsEncodableParam(arg))
                        throw new ArgumentException("All arguments must be convertible to a byte array.");
                    
                    paramTypes[i] = arg.ParameterType;
                }

                var protocol = new RpcProtocol(rpc, paramTypes);
                _protocolsByMethod.Add(rpc, protocol);
                _protocolsById.Add(protocol.Id, protocol);
            }
        }
    }
}