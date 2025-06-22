using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OwlTree
{
    public partial class Connection
    {
        // run on network thread
        private void DecodeIncoming(ClientId source, ReadOnlySpan<byte> bytes, Protocol protocol)
        {
            if (SimulationBuffer.TryDecodeTickMessage(bytes, out var rpcId, out var caller, out var callee, out var tick, out var timestamp))
            {
                if (Logger.includes.rpcReceiveEncodings)
                    Logger.Write("RECEIVING:\n" + SimulationBuffer.TickEncodingSummary(rpcId, caller, callee, tick, timestamp, protocol));
                _simBuffer.AddIncoming(new IncomingMessage
                {
                    caller = caller,
                    callee = LocalId,
                    tick = tick,
                    rpcId = rpcId,
                    protocol = protocol,
                    perms = RpcPerms.AnyToAll,
                    args = new object[] { timestamp }
                });
            }
            else if (NetRole != NetRole.Server && NetworkSpawner.TryDecode(bytes, out rpcId, out var args))
            {
                _simBuffer.AddIncoming(new IncomingMessage
                {
                    caller = source,
                    callee = LocalId,
                    rpcId = rpcId,
                    protocol = Protocol.Tcp,
                    perms = RpcPerms.AuthorityToClients,
                    args = args
                });
                if (Logger.includes.rpcReceiveEncodings)
                {
                    if (rpcId.Id == RpcId.NetworkObjectSpawnId)
                        Logger.Write("RECEIVING:\n" + _spawner.SpawnEncodingSummary((byte)args[0], (NetworkId)args[1]));
                    else
                        Logger.Write("RECEIVING:\n" + _spawner.DespawnEncodingSummary((NetworkId)args[0]));
                }
            }
            else if (Protocols != null && Protocols.TryDecodeRpc(bytes, out rpcId, out caller, out callee, out var target, out args))
            {
                var incoming = new IncomingMessage
                {
                    caller = caller,
                    callee = callee,
                    rpcId = rpcId,
                    target = target,
                    protocol = Protocols.GetSendProtocol(rpcId),
                    perms = Protocols.GetRpcPerms(rpcId),
                    args = args
                };

                if (Logger.includes.rpcReceives)
                {
                    var output = $"RECEIVING:\n{Protocols.GetRpcName(rpcId.Id)} {rpcId}, called on object {target}";
                    if (Logger.includes.rpcReceiveEncodings)
                        output += ":\n" + Protocols.GetEncodingSummary(rpcId, caller, callee, target, args);
                    Logger.Write(output);
                }

                // clients cannot relay messages, or the message isn't allowed to be relayed
                if (IsClient || IsHost || incoming.perms == RpcPerms.ClientsToAuthority)
                {
                    _simBuffer.AddIncoming(incoming);
                    return;
                }

                // determine if the message should be relayed, this will only be run on an authoritative server

                // cannot be intended for the server
                if (incoming.perms == RpcPerms.ClientsToClients)
                {
                    _simBuffer.AddOutgoing(new OutgoingMessage
                    {
                        caller = caller,
                        callee = callee,
                        rpcId = rpcId,
                        protocol = incoming.protocol,
                        perms = incoming.perms,
                        bytes = bytes.ToArray()
                    });
                    return;
                }

                // otherwise perms are ClientsToAll or AnyToAll

                if (Protocols.HasCalleeIdParam(incoming.rpcId))
                {
                    if (incoming.callee == LocalId) // an RPC w/ a callee id param can target the server with ClientId.None
                    {
                        _simBuffer.AddIncoming(incoming);
                    }
                    else // otherwise relay to the intended client
                    {
                        _simBuffer.AddOutgoing(new OutgoingMessage
                        {
                            caller = caller,
                            callee = callee,
                            rpcId = rpcId,
                            protocol = incoming.protocol,
                            perms = incoming.perms,
                            bytes = bytes.ToArray()
                        });
                    }
                    return;
                }

                // otherwise the RPC should be run on the server, and sent to all other clients
                _simBuffer.AddIncoming(incoming);
                _simBuffer.AddOutgoing(new OutgoingMessage
                {
                    caller = caller,
                    callee = callee,
                    rpcId = rpcId,
                    protocol = incoming.protocol,
                    perms = incoming.perms,
                    bytes = bytes.ToArray()
                });
            }
        }

        internal void AddOutgoingMessage(OutgoingMessage m)
        {
            _simBuffer.AddOutgoing(m);
        }

        internal void AddRpc(ClientId callee, RpcId rpcId, NetworkId target, Protocol protocol, object[] args)
        {
            try
            {
                var perms = Protocols.GetRpcPerms(rpcId);
                switch (perms)
                {
                    case RpcPerms.ClientsToAuthority:
                        AddRpcTo(Authority, rpcId, target, protocol, perms, args);
                        break;
                    case RpcPerms.ClientsToClients:
                        if (Protocols.HasCalleeIdParam(rpcId))
                            AddRpcTo(callee, rpcId, target, protocol, perms, args);
                        else
                            AddRpcTo(Clients.Where(c => c != LocalId), rpcId, target, protocol, perms, args);
                        break;
                    case RpcPerms.ClientsToAll:
                    case RpcPerms.AuthorityToClients:
                    case RpcPerms.AnyToAll:
                    default:
                        AddRpcTo(callee, rpcId, target, protocol, perms, args);
                        break;
                }
            }
            catch (Exception e)
            {
                if (Logger.includes.exceptions)
                {
                    var str = new StringBuilder();
                    for (int i = 0; i < args.Length; i++)
                        str.Append($"{i + 1}: {args[i]}\n");
                    Logger.WriteError($"Failed to encode RPC {rpcId}, with arguments:\n{str}", e);
                }
                return;
            }
        }

        private void AddRpcTo(IEnumerable<ClientId> callees, RpcId rpcId, NetworkId target, Protocol protocol, RpcPerms perms, object[] args)
        {
            var bytes = new byte[Protocols.GetRpcByteLength(rpcId, args)];
            Protocols.EncodeRpc(bytes, rpcId, LocalId, ClientId.None, target, args);
            foreach (var callee in callees)
            {
                var message = new OutgoingMessage
                {
                    tick = LocalTick,
                    caller = LocalId,
                    callee = callee,
                    rpcId = rpcId,
                    target = target,
                    protocol = protocol,
                    perms = perms,
                    bytes = RpcEncoding.ChangeRpcCallee(bytes, callee)
                };
                _simBuffer.AddOutgoing(message);

                if (Logger.includes.rpcCalls)
                {
                    var output = $"SENDING:\n{Protocols.GetRpcName(rpcId.Id)} {rpcId}, called on object {target}";
                    if (Logger.includes.rpcCallEncodings)
                        output += ":\n" + Protocols.GetEncodingSummary(rpcId, LocalId, callee, target, args);
                    Logger.Write(output);
                }
            }

            if (Protocols.IsInvokeOnCaller(rpcId))
            {
                var message = new IncomingMessage
                {
                    tick = LocalTick,
                    caller = LocalId,
                    callee = LocalId,
                    rpcId = rpcId,
                    target = target,
                    protocol = protocol,
                    perms = perms,
                    args = args
                };
                _simBuffer.AddIncoming(message);
            }
        }

        private void AddRpcTo(ClientId callee, RpcId rpcId, NetworkId target, Protocol protocol, RpcPerms perms, object[] args)
        {
            var message = new OutgoingMessage
            {
                tick = LocalTick,
                caller = LocalId,
                callee = callee,
                rpcId = rpcId,
                target = target,
                protocol = protocol,
                perms = perms,
                bytes = new byte[Protocols.GetRpcByteLength(rpcId, args)]
            };
            Protocols.EncodeRpc(message.bytes, rpcId, LocalId, callee, target, args);
            _simBuffer.AddOutgoing(message);

            if (Logger.includes.rpcCalls)
            {
                var output = $"SENDING:\n{Protocols.GetRpcName(rpcId.Id)} {rpcId}, called on object {target}";
                if (Logger.includes.rpcCallEncodings)
                    output += ":\n" + Protocols.GetEncodingSummary(rpcId, LocalId, callee, target, args);
                Logger.Write(output);
            }

            if (Protocols.IsInvokeOnCaller(rpcId))
            {
                var incomingMessage = new IncomingMessage
                {
                    tick = LocalTick,
                    caller = LocalId,
                    callee = LocalId,
                    rpcId = rpcId,
                    target = target,
                    protocol = protocol,
                    perms = perms,
                    args = args
                };
                _simBuffer.AddIncoming(incomingMessage);
            }
        }

        internal void AddRpc(ClientId callee, RpcId rpcId, Protocol protocol, object[] args) => AddRpc(callee, rpcId, NetworkId.None, protocol, args);
        internal void AddRpc(RpcId rpcId, object[] args) => AddRpc(ClientId.None, rpcId, NetworkId.None, Protocol.Tcp, args);

        // called in execute queue
        private void InvokeRpc(IncomingMessage message, NetworkObject target)
        {
            try
            {
                Protocols.InvokeRpc(message.caller, message.callee, message.rpcId, target, message.args);
            }
            catch (Exception e)
            {
                if (Logger.includes.exceptions)
                    Logger.WriteError($"Failed to run RPC {(Protocols?.GetRpcName(message.rpcId) ?? "Unknown")} {message.rpcId} on network object: {message.target}.", e);
            }
        }
    }
}