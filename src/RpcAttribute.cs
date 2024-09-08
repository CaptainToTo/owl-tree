
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
    }
}