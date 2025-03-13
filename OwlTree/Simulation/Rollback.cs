using System;
using System.Collections.Generic;
using System.Linq;
using Priority_Queue;

namespace OwlTree
{
    /// <summary>
    /// Resimulates past ticks when new messages from past ticks are received.
    /// </summary>
    public class Rollback : SimulationBuffer
    {
        public Rollback(Logger logger) : base(logger)
        {

        }

        private SimplePriorityQueue<IncomingMessage, uint> _incoming = new();
        private MessageStack _past;
        
        // restores the simulation back to this tick
        private void RewindTo(Tick tick)
        {
            var count = 0;
            foreach (var m in _past.RewindFrom(tick))
            {
                _incoming.Enqueue(m, m.tick);
                count++;
            }
            
            _requiresResimulation = true;
            _resimulateFrom = tick;
        }

        private SimplePriorityQueue<OutgoingMessage, uint> _outgoing = new();

        // tracks what ticks other clients are on
        private Dictionary<ClientId, TickPair> _sessionTicks = new();

        // when incoming messages should stop being provided
        private Tick _exitTick = new Tick(0);

        private Tick _resimulateFrom = new Tick(0);
        private bool _requiresResimulation = false;

        private int _maxTicks;
        private int _tickRate;
        private bool _initialized = false;
        private int _latency;

        private ClientId _localId;
        private ClientId _authority;

        protected override void InitBufferInternal(int tickRate, int latency, uint curTick, ClientId localId, ClientId authority)
        {
            var latencyTicks = (int)MathF.Ceiling((float)latency / tickRate);
            _maxTicks = Math.Max(latencyTicks * 6, 5);
            _presentTick = new Tick(curTick);
            _exitTick = _presentTick.Next();
            _localTick = new Tick(_presentTick.Value + (uint)Math.Max(latencyTicks, 1));
            _past = new MessageStack(Math.Max(latencyTicks * 3, 5));

            _localId = localId;
            _authority = authority;
            _tickRate = tickRate;
            _initialized = _localId == _authority;
            _latency = latency;

            if (_logger.includes.simulationEvents)
            {
                var str = $"Rollback simulation buffer initialized with a tick capacity of {_maxTicks} given a latency of {latency} ms.";
                if (_initialized)
                    str += $"\nAuthority initialized with a local tick of {_localTick}, and a present tick of {_presentTick}";
                _logger.Write(str);
            }
        }

        protected override void NextTickInternal()
        {
            _localTick = _localTick.Next();
            _exitTick = _presentTick.Next();
            if (_logger.includes.simulationEvents)
                _logger.Write($"Simulation moved to next tick. Local tick is {_localTick}, and present tick is {_presentTick}.");

            if (!_initialized) return;

            var tickTcpMessage = new OutgoingMessage{
                tick = _localTick,
                caller = _localId,
                callee = ClientId.None,
                rpcId = new RpcId(RpcId.NextTickId),
                target = NetworkId.None,
                protocol = Protocol.Tcp,
                perms = RpcPerms.AnyToAll,
                bytes = new byte[TickMessageLength]
            };
            var tickUdpMessage = new OutgoingMessage{
                tick = _localTick,
                caller = _localId,
                callee = ClientId.None,
                rpcId = new RpcId(RpcId.NextTickId),
                target = NetworkId.None,
                protocol = Protocol.Udp,
                perms = RpcPerms.AnyToAll,
                bytes = new byte[TickMessageLength]
            };
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            EncodeNextTick(tickTcpMessage.bytes, _localId, ClientId.None, _localTick, timestamp);
            EncodeNextTick(tickUdpMessage.bytes, _localId, ClientId.None, _localTick, timestamp);
            _outgoing.Enqueue(tickTcpMessage, tickTcpMessage.tick);
            _outgoing.Enqueue(tickUdpMessage, tickUdpMessage.tick);

            if (_logger.includes.rpcCallEncodings)
                _logger.Write("SENDING:\n" + TickEncodingSummary(new RpcId(RpcId.NextTickId), _localId, ClientId.None, _localTick, timestamp));
        }

        protected override bool HasOutgoingInternal() => _outgoing.Count > 0;

        protected override void AddOutgoingInternal(OutgoingMessage m)
        {
            m.tick = _initialized ? _localTick : new Tick(0);
            _outgoing.Enqueue(m, m.tick);
        }

        protected override bool TryGetNextOutgoingInternal(out OutgoingMessage m)
        {
            if (!_initialized)
            {
                m = new OutgoingMessage();
                return false;
            }

            if (_outgoing.TryDequeue(out m))
            {
                return true;
            }
            return false;
        }

        protected override void AddIncomingInternal(IncomingMessage m)
        {
            if (!_sessionTicks.ContainsKey(m.caller))
            {
                m.tick = _localTick;
                _incoming.Enqueue(m, m.tick);
                return;
            }

            // initialize non-authority connections
            if (m.rpcId == RpcId.CurTickId)
            {
                if (m.caller != _authority) return;

                if (_logger.includes.rpcReceiveEncodings)
                    _logger.Write("RECEIVING:\n" + TickEncodingSummary(m.rpcId, m.caller, m.callee, m.tick, (long)m.args[0]));

                _latency = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (long)m.args[0]);
                _localTick = new Tick(m.tick.Value + (uint)((float)_latency / _tickRate));
                _presentTick = m.tick;
                _exitTick = _presentTick.Next();
                _initialized = true;

                var tickTcpMessage = new OutgoingMessage{
                    tick = _localTick,
                    caller = _localId,
                    callee = ClientId.None,
                    rpcId = new RpcId(RpcId.NextTickId),
                    target = NetworkId.None,
                    protocol = Protocol.Tcp,
                    perms = RpcPerms.AnyToAll,
                    bytes = new byte[TickMessageLength]
                };
                var tickUdpMessage = new OutgoingMessage{
                    tick = _localTick,
                    caller = _localId,
                    callee = ClientId.None,
                    rpcId = new RpcId(RpcId.NextTickId),
                    target = NetworkId.None,
                    protocol = Protocol.Udp,
                    perms = RpcPerms.AnyToAll,
                    bytes = new byte[TickMessageLength]
                };
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                EncodeNextTick(tickTcpMessage.bytes, _localId, ClientId.None, _localTick, timestamp);
                EncodeNextTick(tickUdpMessage.bytes, _localId, ClientId.None, _localTick, timestamp);
                _outgoing.Enqueue(tickTcpMessage, tickTcpMessage.tick);
                _outgoing.Enqueue(tickUdpMessage, tickUdpMessage.tick);

                if (_logger.includes.simulationEvents)
                    _logger.Write($"Received session tick value from authority of {m.tick}. Compensated for latency, local tick is now {_localTick}.");
                if (_logger.includes.rpcCallEncodings)
                    _logger.Write("SENDING:\n" + TickEncodingSummary(new RpcId(RpcId.NextTickId), _localId, ClientId.None, _localTick, timestamp));
                
                // update tick of any outgoing messages that were enqueued before initialization
                while (_outgoing.TryFirst(out var outgoing) && outgoing.tick == 0)
                {
                    _outgoing.Dequeue();
                    outgoing.tick = _localTick;
                    _outgoing.Enqueue(outgoing, outgoing.tick);
                }
                
                return;
            }

            // a client moved to a new tick
            if (m.rpcId == RpcId.NextTickId)
            {
                _sessionTicks[m.caller].Update(m.protocol, m.tick);
                return;
            }

            m.tick = _sessionTicks[m.caller].Select(m.protocol);

            // if resimulation is required
            if (m.tick < _presentTick && (!_requiresResimulation || m.tick < _resimulateFrom))
            {
                RewindTo(m.tick);
                if (_logger.includes.simulationEvents)
                    _logger.Write($"Received message from past tick {m.tick}, resimulating {_presentTick - m.tick} tick(s) on next ExecuteQueue().");
            }

            _incoming.Enqueue(m, m.tick);
        }

        protected override bool TryGetNextIncomingInternal(out IncomingMessage m)
        {
            if (_incoming.TryFirst(out m))
            {
                _presentTick = m.tick;
                if (m.tick >= _exitTick)
                {
                    if (_requiresResimulation && _logger.includes.simulationEvents)
                        _logger.Write($"Resimulation complete, resimulated from tick {_resimulateFrom} to {_presentTick.Prev()}.");
                    _requiresResimulation = false;
                    return false;
                }
                _incoming.Dequeue();
                _past?.Push(m);
                return true;
            }
            _presentTick = _presentTick.Next();
            _requiresResimulation = false;
            return false;
        }

        protected override void AddTickSourceInternal(ClientId client)
        {
            if (_authority == _localId)
            {
                _sessionTicks.Add(client, new TickPair(_localTick, _localTick));

                var outgoing = new OutgoingMessage{
                    caller = _localId,
                    callee = client,
                    rpcId = new RpcId(RpcId.CurTickId),
                    tick = _localTick,
                    protocol = Protocol.Tcp,
                    perms = RpcPerms.AuthorityToClients,
                    bytes = new byte[TickMessageLength]
                };
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                EncodeCurTick(outgoing.bytes, _localId, client, _localTick, timestamp);
                _outgoing.Enqueue(outgoing, _localTick);

                if (_logger.includes.simulationEvents)
                    _logger.Write($"Sending session tick {_localTick} to {client}.");
                if (_logger.includes.rpcCallEncodings)
                    _logger.Write("SENDING:\n" + TickEncodingSummary(new RpcId(RpcId.CurTickId), _localId, client, _localTick, timestamp));
            }
            else
            {
                var startTick = new Tick(_localTick - (uint)((float)_latency / _tickRate));
                _sessionTicks.Add(client, new TickPair(startTick, startTick));
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