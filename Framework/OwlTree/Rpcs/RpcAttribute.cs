
using System;

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
        /// Both server and client connections can call this RPC.
        /// </summary>
        Any
    }

    /// <summary>
    /// Provides the callee the ClientId of the caller.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class RpcCallerAttribute : Attribute { }

    /// <summary>
    /// Provides the caller a way to specify a specific client as the callee.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class RpcCalleeAttribute : Attribute { }

    /// <summary>
    /// Manually assign an id value to RPCs.
    /// Setting this manually ensures the id matches across different programs.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class AssignRpcIdAttribute : Attribute {
        public uint Id = 0;

        public AssignRpcIdAttribute(uint id)
        {
            Id = id;
        }

        public AssignRpcIdAttribute(int id)
        {
            Id = (uint)id;
        }
    }

    /// <summary>
    /// Mark a static class as a registry for RPC and type ids, using consts and enums.
    /// There should only ever be 1 IdRegistry per project.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class IdRegistryAttribute : Attribute { }

    /// <summary>
    /// Tag a method as an RPC. All parameters must be encodable as a byte array, and the return type must be void.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class RpcAttribute : Attribute
    {
        public RpcCaller caller = RpcCaller.Any;

        /// <summary>
        /// Whether this RPC is delivered through TCP or UDP.
        /// </summary>
        public Protocol RpcProtocol = Protocol.Tcp;

        /// <summary>
        /// Whether the method should also be run on the caller. <b>Default = false</b>
        /// </summary>
        public bool InvokeOnCaller = false;

        /// <summary>
        /// Tag a method as an RPC. All parameters must be encodable as a byte array, and the return type must be void.
        /// </summary>
        public RpcAttribute(RpcCaller caller = RpcCaller.Any)
        {
            this.caller = caller;
        }
    }
}