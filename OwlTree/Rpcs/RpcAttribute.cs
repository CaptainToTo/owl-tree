
using System;

namespace OwlTree
{
    /// <summary>
    /// Who is allowed to call the RPC.
    /// </summary>
    public enum RpcPerms
    {
        /// <summary>
        /// Only the authority, whether server or host, is allowed to call this RPC.
        /// It can be sent any and all clients.
        /// </summary>
        AuthorityToClients,
        /// <summary>
        /// Only clients are allowed to call this RPC.
        /// It can only be sent to the authority.
        /// </summary>
        ClientsToAuthority,
        /// <summary>
        /// Only clients are allowed to call this RPC.
        /// It cannot be sent to the authority, only to other clients.
        /// </summary>
        ClientsToClients,
        /// <summary>
        /// Only clients are allowed to call this RPC.
        /// It can be sent to every- and anyone.
        /// </summary>
        ClientsToAll,
        /// <summary>
        /// Anyone can call this RPC, and send it to every- and anyone.
        /// </summary>
        AnyToAll
    }

    /// <summary>
    /// Provides the callee the ClientId of the caller.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class CallerIdAttribute : Attribute { }

    /// <summary>
    /// Provides the caller a way to specify a specific client as the callee.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class CalleeIdAttribute : Attribute { }

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
    /// Tag a method as an RPC. All parameters must be encodable, the method must be virtual, and the return type must be void.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class RpcAttribute : Attribute
    {
        public RpcPerms caller = RpcPerms.AnyToAll;

        /// <summary>
        /// Whether this RPC is delivered through TCP or UDP.
        /// </summary>
        public Protocol RpcProtocol = Protocol.Tcp;

        /// <summary>
        /// Whether the method should also be run on the caller. <b>Default = false</b>
        /// </summary>
        public bool InvokeOnCaller = false;

        /// <summary>
        /// Tag a method as an RPC. All parameters must be encodable, the method must be virtual, and the return type must be void.
        /// </summary>
        public RpcAttribute(RpcPerms caller = RpcPerms.AnyToAll)
        {
            this.caller = caller;
        }
    }
}