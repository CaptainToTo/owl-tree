using System;
using System.Collections.Generic;
using System.Linq;
using Priority_Queue;

namespace OwlTree
{
    /// <summary>
    /// Messages are sorted by tick, and stop between each tick.
    /// Past messages are lost, and no rollback or synchronization occurs.
    /// </summary>
    public class Lockstep : SimulationBuffer
    {

        private SimplePriorityQueue<IncomingMessage, uint> _incoming = new();
        private SimplePriorityQueue<OutgoingMessage, uint> _outgoing = new();

        private Dictionary<ClientId, TickPair> _sessionTicks = new();

        private Tick _nextTick = new Tick(0);
        private Tick _lastCompleteTick = new Tick(0);

        private int _maxTicks;
        private bool _requireCatchup = false;
        private int _tickRate;

        private ClientId _localId;
        private ClientId _authority;

        private bool _initialized = false;
        private bool _synced = false;

        public Lockstep(Logger logger) : base(logger)
        {
        }

        protected override bool HasOutgoingInternal() => _outgoing.Count > 0;

        protected override void InitBufferInternal(int tickRate, int latency, uint curTick, ClientId localId, ClientId authority)
        {
            _maxTicks = Math.Max((int)MathF.Ceiling((float)latency / tickRate * 6f), 5);
            CurTick = new Tick(curTick);
            _localId = localId;
            _authority = authority;
            _initialized = true;
            _synced = _localId == _authority;
            _tickRate = tickRate; 

            if (_logger.includes.simulationEvents)
                _logger.Write($"Lockstep simulation buffer initialized with a tick capacity of {_maxTicks} given a latency of {latency} ms.");
        }
        
        protected override void NextTickInternal()
        {
            CurTick = CurTick.Next();
            if (_logger.includes.simulationEvents)
                _logger.Write($"Simulation moved to next tick: {CurTick}.");

            if (!_initialized || !_synced) return;

            var tickTcpMessage = new OutgoingMessage{
                tick = CurTick,
                caller = _localId,
                callee = ClientId.None,
                rpcId = new RpcId(RpcId.NextTickId),
                target = NetworkId.None,
                protocol = Protocol.Tcp,
                perms = RpcPerms.AnyToAll,
                bytes = new byte[TickMessageLength]
            };
            var tickUdpMessage = new OutgoingMessage{
                tick = CurTick,
                caller = _localId,
                callee = ClientId.None,
                rpcId = new RpcId(RpcId.NextTickId),
                target = NetworkId.None,
                protocol = Protocol.Udp,
                perms = RpcPerms.AnyToAll,
                bytes = new byte[TickMessageLength]
            };
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            EncodeNextTick(tickTcpMessage.bytes, _localId, ClientId.None, CurTick, timestamp);
            EncodeNextTick(tickUdpMessage.bytes, _localId, ClientId.None, CurTick, timestamp);
            _outgoing.Enqueue(tickTcpMessage, tickTcpMessage.tick);
            _outgoing.Enqueue(tickUdpMessage, tickUdpMessage.tick);

            if (_logger.includes.rpcCallEncodings)
                _logger.Write("SENDING:\n" + TickEncodingSummary(new RpcId(RpcId.NextTickId), _localId, ClientId.None, CurTick, timestamp));
        }

        protected override void AddIncomingInternal(IncomingMessage m)
        {
            // invoke on caller
            if (m.caller == _localId)
            {
                _incoming.Enqueue(m, m.tick);
                return;
            }

            if (!_sessionTicks.ContainsKey(m.caller))
            {
                m.tick = _nextTick;
                _incoming.Enqueue(m, m.tick);
                return;
            }

            if (m.rpcId == RpcId.CurTickId)
            {
                if (m.caller != _authority) return;

                if (_logger.includes.rpcReceiveEncodings)
                    _logger.Write("RECEIVING:\n" + TickEncodingSummary(m.rpcId, m.caller, m.callee, m.tick, (long)m.args[0]));

                var latency = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (long)m.args[0];
                CurTick = new Tick(m.tick.Value + (uint)((float)latency / _tickRate));
                _nextTick = CurTick;

                var tickTcpMessage = new OutgoingMessage{
                    tick = CurTick,
                    caller = _localId,
                    callee = ClientId.None,
                    rpcId = new RpcId(RpcId.NextTickId),
                    target = NetworkId.None,
                    protocol = Protocol.Tcp,
                    perms = RpcPerms.AnyToAll,
                    bytes = new byte[TickMessageLength]
                };
                var tickUdpMessage = new OutgoingMessage{
                    tick = CurTick,
                    caller = _localId,
                    callee = ClientId.None,
                    rpcId = new RpcId(RpcId.NextTickId),
                    target = NetworkId.None,
                    protocol = Protocol.Udp,
                    perms = RpcPerms.AnyToAll,
                    bytes = new byte[TickMessageLength]
                };
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                EncodeNextTick(tickTcpMessage.bytes, _localId, ClientId.None, CurTick, timestamp);
                EncodeNextTick(tickUdpMessage.bytes, _localId, ClientId.None, CurTick, timestamp);
                _outgoing.Enqueue(tickTcpMessage, tickTcpMessage.tick);
                _outgoing.Enqueue(tickUdpMessage, tickUdpMessage.tick);
                _synced = true;

                if (_logger.includes.simulationEvents)
                    _logger.Write($"Received session tick value from authority of {m.tick}. Compensated for latency, current tick is now {CurTick}.");
                if (_logger.includes.rpcCallEncodings)
                    _logger.Write("SENDING:\n" + TickEncodingSummary(new RpcId(RpcId.NextTickId), _localId, ClientId.None, CurTick, timestamp));
                
                return;
            }

            if (m.rpcId == RpcId.NextTickId)
            {
                var prevTick = _sessionTicks[m.caller].Select(m.protocol);

                _sessionTicks[m.caller].Update(m.protocol, m.tick);

                var newTick = _sessionTicks[m.caller].Select(m.protocol);

                if (prevTick < _sessionTicks.Min(p => p.Value.Min().Value))
                {
                    _incoming.Enqueue(new IncomingMessage{
                        caller = ClientId.None,
                        callee = _localId,
                        rpcId = new RpcId(RpcId.EndTickId),
                        tick = prevTick
                    }, prevTick);
                    _lastCompleteTick = prevTick;

                    if (_logger.includes.simulationEvents)
                        _logger.Write($"Received all messages for tick {_lastCompleteTick}.");

                    _requireCatchup = _nextTick < _lastCompleteTick && _lastCompleteTick - _nextTick > _maxTicks;

                    if (_requireCatchup && _logger.includes.simulationEvents)
                        _logger.Write($"Simulation is too far behind (current simulated tick: {_nextTick}, newest tick: {_lastCompleteTick}), catching up.");
                }

                return;
            }

            m.tick = _sessionTicks[m.caller].Select(m.protocol);
            _incoming.Enqueue(m, m.tick);
        }

        protected override void AddOutgoingInternal(OutgoingMessage m)
        {
            m.tick = CurTick;
            _outgoing.Enqueue(m, m.tick);
        }

        protected override bool TryGetNextIncomingInternal(out IncomingMessage m)
        {
            if (_sessionTicks.Count > 0 && _nextTick > _lastCompleteTick)
            {
                m = new IncomingMessage();
                return false;
            }

            if (_incoming.TryDequeue(out m))
            {
                if (m.rpcId == RpcId.EndTickId)
                {
                    // if (_requireCatchup && _incoming.TryDequeue(out m))
                    //     return true;
                    _requireCatchup = false;
                    _nextTick = _nextTick.Next();
                    return false;
                }
                return true;
            }
            _requireCatchup = false;
            return false;
        }

        protected override bool TryGetNextOutgoingInternal(out OutgoingMessage m)
        {
            return _outgoing.TryDequeue(out m);
        }

        protected override void AddTickSourceInternal(ClientId client)
        {
            _sessionTicks.Add(client, new TickPair(CurTick, CurTick));

            if (_authority == _localId)
            {
                var outgoing = new OutgoingMessage{
                    caller = _localId,
                    callee = client,
                    rpcId = new RpcId(RpcId.CurTickId),
                    tick = _nextTick,
                    protocol = Protocol.Tcp,
                    perms = RpcPerms.AuthorityToClients,
                    bytes = new byte[TickMessageLength]
                };
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                EncodeCurTick(outgoing.bytes, _localId, client, CurTick, timestamp);
                _outgoing.Enqueue(outgoing, CurTick);

                if (_sessionTicks.Count == 1)
                    _nextTick = CurTick.Next();

                if (_logger.includes.simulationEvents)
                    _logger.Write($"Sending session tick {CurTick} to {client}.");
                if (_logger.includes.rpcCallEncodings)
                    _logger.Write("SENDING:\n" + TickEncodingSummary(new RpcId(RpcId.CurTickId), _localId, client, CurTick, timestamp));
            }
        }

        protected override void RemoveTickSourceInternal(ClientId client)
        {
            _sessionTicks.Remove(client);
        }

        protected override void UpdateAuthorityInternal(ClientId authority)
        {
            _authority = authority;
        }
    }
}