
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PostSharp.Aspects;
using PostSharp.Serialization;

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
    [AttributeUsage(AttributeTargets.Parameter)]
    public class RpcCallerAttribute : Attribute { }

    /// <summary>
    /// Provides the caller a way to specify a specific client as the callee.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class RpcCalleeAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class AssignRpcIdAttribute : Attribute {
        public ushort Id = 0;

        public AssignRpcIdAttribute(ushort id)
        {
            Id = id;
        }
    }

    /// <summary>
    /// Tag a method as an RPC. All parameters must be encodable as a byte array, and the return type must be void.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false), PSerializable]
    public class RpcAttribute : MethodInterceptionAspect
    {
        public RpcCaller caller = RpcCaller.Server;

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
        public RpcAttribute(RpcCaller caller)
        {
            this.caller = caller;
        }

        private static Dictionary<MethodInfo, RpcProtocol> _protocolsByMethod = new Dictionary<MethodInfo, RpcProtocol>();
        private static Dictionary<RpcId, RpcProtocol> _protocolsById = new Dictionary<RpcId, RpcProtocol>();

        private static bool _initialized = false;

        Type netObjType = typeof(NetworkObject);

        public static void GenerateRpcProtocols(Logger logger)
        {
            if (_initialized) return;
            _initialized = true;

            logger.Write(Logger.LogRule.Verbose, "Generating RPC Protocols =====\n");

            IEnumerable<Type> types = NetworkObject.GetNetworkObjectTypes();

            foreach (var t in types)
            {
                var rpcs = t.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                            .Where(m => m.Name.ToLower().Contains("rpc") && !m.Name.Contains("<"));
                
                foreach (var rpc in rpcs)
                {
                    if (rpc.ReturnType != typeof(void))
                        throw new InvalidOperationException("RPC return types must be void.");

                    var args = rpc.GetParameters();
                    Type[] paramTypes = new Type[args.Length];

                    for (int i = 0; i < args.Length; i++)
                    {
                        var arg = args[i];

                        if (!OwlTree.RpcProtocol.IsEncodableParam(arg))
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

        public override void OnInvoke(MethodInterceptionArgs args)
        {
            if (!netObjType.IsAssignableFrom(args.Instance.GetType()))
                throw new InvalidOperationException("RPCs cannot be called on non-NetworkObjects.");
            
            var netObj = (NetworkObject)args.Instance;

            if (!netObj.IsActive)
                throw new InvalidOperationException("RPCs can only be called on active network objects.");

            if (netObj.Connection == null || !netObj.Connection.IsActive)
                throw new InvalidOperationException("RPCs can only be called on an active connection.");
            
            if (
                (caller == (RpcCaller)netObj.Connection.NetRole) ||
                (caller == RpcCaller.Any && !netObj.IsReceivingRpc)
            )
            {
                var method = args.Method as MethodInfo;

                if (method == null)
                    throw new InvalidOperationException("RPC does not exist");

                if (!_protocolsByMethod.TryGetValue(method, out var protocol))
                    throw new InvalidOperationException("RPC protocol does not exist.");
                
                var paramList = method.GetParameters();
                var argsList = args.Arguments.ToArray();
                ClientId callee = ClientId.None;

                for (int i = 0; i < paramList.Length; i++)
                {
                    if (paramList[i].CustomAttributes.Any(a => a.AttributeType == typeof(RpcCallerAttribute)))
                        args.Arguments.SetArgument(i, netObj.Connection.LocalId);
                    else if (paramList[i].CustomAttributes.Any(a => a.AttributeType == typeof(RpcCalleeAttribute)))
                        callee = (ClientId)argsList[i];
                }
                
                netObj.OnRpcCall?.Invoke(callee, protocol.Id, netObj.Id, this.RpcProtocol, argsList);

                if (InvokeOnCaller)
                    args.Proceed();
            }
            else if (netObj.IsReceivingRpc)
            {
                args.Proceed();
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