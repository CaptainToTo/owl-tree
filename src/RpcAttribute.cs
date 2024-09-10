
using System.Reflection;

namespace OwlTree
{
    public enum RpcCaller
    {
        Server,
        Client,
        Any
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class RpcAttribute : Attribute
    {
        public RpcCaller caller = RpcCaller.Any;

        public RpcAttribute(RpcCaller caller)
        {
            this.caller = caller;
        }

        // private static 

        public static void GenerateRpcProtocols()
        {
            Type t = typeof(NetworkObject);

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
                }
            }
        }
    }
}