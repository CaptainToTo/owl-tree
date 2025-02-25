using System;
using System.Collections.Generic;
using System.Linq;
using Priority_Queue;

namespace OwlTree
{
    public class Snapshot : SimulationBuffer
    {
        private class TickPair
        {
            public Tick Tcp;
            public Tick Udp;

            public TickPair(Tick tcp, Tick udp)
            {
                Tcp = tcp;
                Udp = udp;
            }

            public Tick Select(Protocol protocol) => protocol == Protocol.Tcp ? Tcp : Udp;

            public void Update(Protocol protocol, Tick tick)
            {
                if (protocol == Protocol.Tcp)
                    Tcp = tick;
                else
                    Udp = tick;
            }

            public Tick Min() => Tcp < Udp ? Tcp : Udp;
            public Tick Max() => Tcp > Udp ? Tcp : Udp;
        }

        private SimplePriorityQueue<IncomingMessage, uint> _incoming = new();
        private SimplePriorityQueue<OutgoingMessage, uint> _outgoing = new();

        private Dictionary<ClientId, TickPair> _sessionTicks = new();

        private Tick _newestTick = new Tick(0);

        private int _maxTicks;
        private bool _requireCatchup = false;

        private ClientId _localId;
        private bool _isAuthority;

        protected override bool HasOutgoingInternal() => _outgoing.Count > 0;

        protected override void InitBufferInternal(int tickRate, int latency, uint curTick, ClientId localId, bool isAuthority)
        {
            _maxTicks = (int)MathF.Ceiling((float)latency / tickRate * 1.5f);
            CurTick = new Tick(curTick);
            _newestTick = CurTick;
            _localId = localId;
            _isAuthority = isAuthority;
        }
        
        protected override void NextTickInternal()
        {
            CurTick.Next();
            var tickMessage = new OutgoingMessage{
                tick = CurTick,
                caller = _localId,
                callee = ClientId.None,
                rpcId = new RpcId(RpcId.NextTickId),
                target = NetworkId.None,
                protocol = Protocol.Tcp,
                perms = RpcPerms.AnyToAll,
                bytes = new byte[TickMessageLength]
            };
            EncodeNextTick(tickMessage.bytes, _localId, CurTick);
            AddOutgoing(tickMessage);
        }

        protected override void AddIncomingInternal(IncomingMessage m)
        {
            if (!_sessionTicks.ContainsKey(m.caller))
            {
                m.tick = CurTick;
                _incoming.Enqueue(m, m.tick);
                return;
            }

            if (m.rpcId == RpcId.CurTickId)
                CurTick = m.tick;

            if (m.rpcId == RpcId.NextTickId)
            {
                var prevTick = _sessionTicks[m.caller].Select(m.protocol);

                _sessionTicks[m.caller].Update(m.protocol, m.tick);

                var newTick = _sessionTicks[m.caller].Select(m.protocol);

                if (_newestTick < newTick)
                    _newestTick = newTick;
                
                _requireCatchup = _newestTick - CurTick > _maxTicks;

                if (prevTick < _sessionTicks.Min(p => p.Value.Min()))
                {
                    _incoming.Enqueue(new IncomingMessage{
                        caller = ClientId.None,
                        callee = _localId,
                        rpcId = new RpcId(RpcId.EndTickId),
                        tick = prevTick
                    }, prevTick);
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
            if (_incoming.TryDequeue(out m))
            {
                if (m.rpcId == RpcId.EndTickId && !_requireCatchup)
                    return false;
                return true;
            }
            return false;
        }

        protected override bool TryGetNextOutgoingInternal(out OutgoingMessage m)
        {
            return _outgoing.TryDequeue(out m);
        }

        protected override void AddTickSourceInternal(ClientId client)
        {
            _sessionTicks.Add(client, new TickPair(CurTick, CurTick));

            if (_isAuthority)
            {
                var outgoing = new OutgoingMessage{
                    caller = _localId,
                    callee = client,
                    rpcId = new RpcId(RpcId.CurTickId),
                    tick = CurTick,
                    protocol = Protocol.Tcp,
                    perms = RpcPerms.AuthorityToClients,
                    bytes = new byte[TickMessageLength]
                };
                EncodeCurTick(outgoing.bytes, _localId, CurTick);
                _outgoing.Enqueue(outgoing, CurTick);
            }
        }

        protected override void RemoveTickSourceInternal(ClientId client)
        {
            _sessionTicks.Remove(client);
        }

        protected override void UpdateAuthorityInternal(bool isAuthority)
        {
            _isAuthority = isAuthority;
        }
    }
}