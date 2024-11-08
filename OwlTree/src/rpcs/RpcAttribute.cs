
using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Marks this enum as intended to assign RPC ids. 
    /// Enum values will be solved at compile-time to generate ids.
    /// Enums used to assign an RPC id with the <c>AssignRpcId()</c>
    /// attribute must have this attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Enum, AllowMultiple = false)]
    public class RpcIdEnumAttribute : Attribute { }

    /// <summary>
    /// Marks this constant as a RPC id value.
    /// Consts used to set RPC ids with the <c>AssignRpcId()</c>
    /// attribute, or enums labeled with the <c>RpcIdEnum()</c> attribute
    /// must have this attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class RpcIdConstAttribute : Attribute { }

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

        private static Dictionary<MethodInfo, RpcProtocol> _protocolsByMethod = new Dictionary<MethodInfo, RpcProtocol>();
        private static Dictionary<RpcId, RpcProtocol> _protocolsById = new Dictionary<RpcId, RpcProtocol>();

        private static bool _initialized = false;

        static Type netObjType = typeof(NetworkObject);

        public static void GenerateRpcProtocols(Logger logger)
        {
            if (_initialized) return;
            _initialized = true;

            logger.Write(Logger.LogRule.Verbose, "Generating RPC Protocols =====\n");

            IEnumerable<Type> types = NetworkObject.GetNetworkObjectTypes();

            foreach (var t in types)
            {
                var rpcs = t.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                            .Where(m => m.GetCustomAttribute<RpcAttribute>() != null).ToArray()
                            .OrderBy(m => (m.GetCustomAttribute<AssignRpcIdAttribute>() != null ? "0" : "1") + m.Name);
                
                foreach (var rpc in rpcs)
                {
                    if (rpc.ReturnType != typeof(void))
                        throw new InvalidOperationException("RPC return types must be void.");

                    var args = rpc.GetParameters();
                    Type[] paramTypes = new Type[args.Length];

                    for (int i = 0; i < args.Length; i++)
                    {
                        var arg = args[i];

                        if (!OwlTree.RpcProtocol.IsEncodableParam(arg.ParameterType))
                            throw new ArgumentException("All arguments must be convertible to a byte array.");
                        
                        paramTypes[i] = arg.ParameterType;
                    }

                    var assignedId = rpc.GetCustomAttribute<AssignRpcIdAttribute>();

                    if (assignedId != null)
                    {
                        var protocol = new RpcProtocol(t, rpc, paramTypes, assignedId.Id);
                        _protocolsByMethod.Add(rpc, protocol);
                        _protocolsById.Add(protocol.Id, protocol);
                        if (logger.IncludesVerbose)
                            logger.Write(Logger.LogRule.Verbose, protocol.ToString());
                    }
                    else
                    {
                        var protocol = new RpcProtocol(t, rpc, paramTypes);
                        _protocolsByMethod.Add(rpc, protocol);
                        _protocolsById.Add(protocol.Id, protocol);
                        if (logger.IncludesVerbose)
                            logger.Write(Logger.LogRule.Verbose, protocol.ToString());
                    }
                }
            }

            logger.Write(Logger.LogRule.Verbose, "Completed RPC Protocols ======");
        }

        public static bool IsRpc(MethodInfo method)
        {
            return _protocolsByMethod.ContainsKey(method);
        }

        public static bool OnInvoke(MethodInfo method, NetworkObject netObj, object[] argsList)
        {
            if (!netObj.IsActive)
                throw new InvalidOperationException("RPCs can only be called on active network objects.");

            if (netObj.Connection == null)
                throw new InvalidOperationException("RPCs can only be called on an active connection.");

            var attr = method.GetCustomAttribute<RpcAttribute>();
            
            if (
                (attr.caller == (RpcCaller)netObj.Connection.NetRole) ||
                (attr.caller == RpcCaller.Any && !netObj.IsReceivingRpc)
            )
            {
                if (method == null)
                    throw new InvalidOperationException("RPC does not exist");

                if (!_protocolsByMethod.TryGetValue(method, out var protocol))
                    throw new InvalidOperationException("RPC protocol does not exist.");
                
                var paramList = method.GetParameters();
                ClientId callee = ClientId.None;

                for (int i = 0; i < paramList.Length; i++)
                {
                    if (paramList[i].CustomAttributes.Any(a => a.AttributeType == typeof(RpcCallerAttribute)))
                        argsList[i] = netObj.Connection.LocalId;
                    else if (paramList[i].CustomAttributes.Any(a => a.AttributeType == typeof(RpcCalleeAttribute)))
                        callee = (ClientId)argsList[i];
                }
                
                netObj.OnRpcCall?.Invoke(callee, protocol.Id, netObj.Id, attr.RpcProtocol, argsList);

                if (attr.InvokeOnCaller)
                    return true;
                return false;
            }
            else if (netObj.IsReceivingRpc)
            {
                return true;
            }
            else
            {
                throw new InvalidOperationException("This connection does not have the permission to call this RPC.");
            }
        }

        public static int RpcExpectedLength(RpcId id, object[] args)
        {
            return _protocolsById[id].ExpectedLength(args);
        }

        public static void EncodeRpc(Span<byte> bytes, RpcId id, NetworkId source, object[] args)
        {
            _protocolsById[id].Encode(bytes, source, args);
        }

        public static bool TryDecodeRpc(ClientId source, ReadOnlySpan<byte> bytes, out RpcId rpcId, out NetworkId target, out object[] args)
        {
            rpcId = new RpcId(bytes);
            if (_protocolsById.ContainsKey(rpcId))
            {
                args = _protocolsById[rpcId].Decode(source, bytes, out target);
                return true;
            }
            target = NetworkId.None;
            args = new object[0];
            return false;
        }

        public static void InvokeRpc(RpcId id, NetworkObject target, object[] args)
        {
            _protocolsById[id].Invoke(target, args);
        }

        public static string GetEncodingSummary(RpcId id, NetworkId target, object[] args, ClientId localId)
        {
            return _protocolsById[id].GetEncodingSummary(target, args, localId);
        }
    }
}