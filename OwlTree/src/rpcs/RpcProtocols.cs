
using System;
using System.Linq;
using System.Text;

namespace OwlTree
{
    /// <summary>
    /// Contains the protocols for each RPC is the application project, and helpers for encoding and decoding RPCs.
    /// Protocols are generated by OwlTree source generator.
    /// </summary>
    public abstract class RpcProtocols
    {
        // TODO: remove reflection usage
        /// <summary>
        /// Gets the specific project implementation.
        /// </summary>
        internal static RpcProtocols GetProjectImplementation()
        {
            return (RpcProtocols)Activator.CreateInstance(
                AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .Where(t => t.IsClass && !t.IsAbstract && typeof(RpcProtocols).IsAssignableFrom(t)).FirstOrDefault()
                );
        }

        // overrides generated at compile time ==

        /// <summary>
        /// Returns an array containing all of RPC id values for user created RPCs.
        /// </summary>
        public abstract uint[] GetRpcIds();

        /// <summary>
        /// Returns the parameter types associated with the given RPC id value.
        /// Returns null if no such RPC exists.
        /// </summary>
        public abstract Type[] GetProtocol(uint rpcId);

        /// <summary>
        /// Returns the name of the method associated with the given RPC id value.
        /// Returns an empty string if no such RPC exists.
        /// </summary>
        public abstract string GetRpcName(uint rpcId);

        /// <summary>
        /// Returns the name of the method parameter at the given paramInd, 
        /// for the given RPC. Returns an empty string no such parameter exists.
        /// </summary>
        public abstract string GetRpcParamName(uint rpcId, int paramInd);
        
        /// <summary>
        /// Gets the RpcCallee parameter index on the given RPC. If the RPC doesn't have 
        /// a RpcCallee parameter, returns -1.
        /// </summary>
        public abstract int GetRpcCalleeParam(uint rpcId);

        /// <summary>
        /// Gets the RpcCaller parameter index on the given RPC. If the RPC doesn't have 
        /// a RpcCaller parameter, returns -1.
        /// </summary>
        public abstract int GetRpcCallerParam(uint rpcId);

        /// <summary>
        /// Returns who is allowed to call the given RPC.
        /// </summary>
        public abstract RpcCaller GetRpcCaller(uint rpcId);

        /// <summary>
        /// Returns whether the given RPC is sent through UDP or TCP.
        /// </summary>
        public abstract Protocol GetSendProtocol(uint rpcId);

        /// <summary>
        /// Returns true if the given RPC is marked to also execute on the caller connection.
        /// </summary>
        public abstract bool IsInvokeOnCaller(uint rpcId);
        
        /// <summary>
        /// Invokes the specific method assigned with the given RPC id, on the given target NetworkObject.
        /// </summary>
        protected abstract void InvokeRpc(uint rpcId, NetworkObject target, object[] args);

        // ======================================

        /// <summary>
        /// Checks if the parameter at the given index, for the given RPC, is a
        /// <c>RpcCallee</c> parameter.
        /// </summary>
        public bool IsRpcCalleeParam(uint rpcId, int paramInd)
        {
            var ind = GetRpcCalleeParam(rpcId);
            return ind != -1 && ind == paramInd;
        }

        /// <summary>
        /// Checks if the parameter at the given index, for the given RPC, is a
        /// <c>RpcCaller</c> parameter.
        /// </summary>
        public bool IsRpcCallerParam(uint rpcId, int paramInd)
        {
            var ind = GetRpcCallerParam(rpcId);
            return ind != -1 && ind == paramInd;
        }

        public bool TryDecodeRpc(ClientId source, ReadOnlySpan<byte> bytes, out RpcId rpcId, out NetworkId target, out object[] args)
        {
            rpcId = new RpcId(bytes);
            var paramTypes = GetProtocol(rpcId.Id);
            if (paramTypes != null)
            {
                args = RpcEncoding.DecodeRpc(source, bytes, paramTypes, GetRpcCallerParam(rpcId.Id), out target);
                return true;
            }
            target = NetworkId.None;
            args = null;
            return false;
        }
        
        /// <summary>
        /// Invokes the given RPC on the given NetworkObject, passing the given args to that method.
        /// </summary>
        internal void InvokeRpc(ClientId caller, ClientId callee, RpcId id, NetworkObject target, object[] args)
        {
            if (!ValidateArgs(id, args))
                throw new ArgumentException("Provided arguments do not match the RPC function signature.");
            
            var ind = GetRpcCallerParam(id.Id);
            if (ind != -1)
                args[ind] = caller;
            ind = GetRpcCalleeParam(id.Id);
            if (ind != -1)
                args[ind] = callee;

            target.i_IsReceivingRpc = true;
            InvokeRpc(id.Id, target, args);
            target.i_IsReceivingRpc = false;
        }

        /// <summary>
        /// Checks if the given args match the required types for the given RPC.
        /// </summary>
        public bool ValidateArgs(RpcId id, object[] args)
        {
            var paramTypes = GetProtocol(id.Id);
            if (paramTypes == null)
                return false;
            
            // if args is null, assume there are no args, in which case there may be no parameters
            if (args == null && paramTypes.Length == 0)
                return true;
            // otherwise there must be the same number of args and parameters
            if (args.Length != paramTypes.Length)
                return false;
            
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].GetType() != paramTypes[i])
                    return false;
            }

            return true;
        }

        public string GetAllProtocolSummaries()
        {
            var ids = GetRpcIds();
            var str = new StringBuilder("All RPC Protocols:\n");
            foreach (var id in ids)
            {
                str.Append(GetProtocolSummary(new RpcId(id))).Append("\n====\n");
            }
            return str.ToString();
        }

        /// <summary>
        /// Builds a string representation of the given RPC's protocol.
        /// This shows how arguments are laid out in the RPC's byte encoding.
        /// </summary>
        public string GetProtocolSummary(RpcId id)
        {
            string title = GetRpcName(id.Id) + " " + id + ":\n";
            var encoding = new StringBuilder();
            encoding.Append("  Bytes: [ RpcId:")
                .Append(RpcId.MaxLength())
                .Append("b ][ NetId:")
                .Append(NetworkId.MaxLength())
                .Append("b ]");
            var paramStr = new StringBuilder();

            int maxSize = RpcId.MaxLength() + NetworkId.MaxLength();

            var parameters = GetProtocol(id.Id);
            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                int size = RpcEncoding.GetMaxLength(param);
                maxSize += size;
                encoding.Append($"[ {i + 1}:{size}b ]");
                paramStr.Append($"   {i + 1}: {param} ").Append(GetRpcParamName(id.Id, i));

                if (IsRpcCalleeParam(id.Id, i))
                    paramStr.Append(" [Callee]");
                else if (IsRpcCallerParam(id.Id, i))
                    paramStr.Append(" [Caller]");
                
                paramStr.Append("\n");
            }

            return encoding.Insert(0, title).Append(" = ").Append(maxSize).Append(" max bytes\n")
                .Append(paramStr).ToString();
        }

        /// <summary>
        /// Builds a string containing a breakdown of how the given arguments will be 
        /// ordered in the RPC's byte encoding. The caller argument is used to replace a 
        /// <c>RpcCaller</c> argument if there is one.
        /// </summary>
        public string GetEncodingSummary(ClientId caller, RpcId id, NetworkId target, object[] args)
        {
            if (!ValidateArgs(id, args))
                return "Invalid Args...";

            int len = RpcEncoding.GetExpectedRpcLength(args);
            byte[] bytes = new byte[len];

            var ind = GetRpcCallerParam(id.Id);
            if (ind != -1 && args != null && args.Length > ind)
                args[ind] = caller;

            RpcEncoding.EncodeRpc(bytes, id, target, args);

            var str = new StringBuilder($"     Bytes: {BitConverter.ToString(bytes)}\n");
            str.Append("  Encoding: |__RpcId__| |__NetId__|");

            if (args != null && args.Length > 0)
            {
                var argsStr = new StringBuilder();

                for (int i = 0; i < args.Length; i++)
                {
                    var arg = args[i];

                    int size = RpcEncoding.GetExpectedLength(arg);
                    int strLen = (size * 2) + (size - 1);
                    string iStr = (i + 1).ToString();
                    int front = (strLen / 2) - 1;
                    int back = (strLen / 2) - (1 + iStr.Length) + 1;

                    str.Append($" |{new string('_', front)}{iStr}{new string('_', back)}|");
                    argsStr.Append($"    ({iStr}) {arg.GetType()} {GetRpcParamName(id.Id, i)}: {arg}\n");
                }

                str.Append("\n").Append(argsStr);
            }

            return str.ToString();
        }
    }
}