using Priority_Queue;

namespace OwlTree
{
    public class Snapshot : SimulationBuffer
    {
        private SimplePriorityQueue<IncomingMessage, uint> _incoming = new();
        private SimplePriorityQueue<OutgoingMessage, uint> _outgoing = new();

        Tick curReceivingTick = new Tick(0);

        private int _maxTicks;
        private int _curTicks;
        private bool _requireCatchup;

        public Snapshot(int maxTicks)
        {
            _maxTicks = maxTicks;
            _curTicks = 0;
            _requireCatchup = false;
        }

        public override bool HasOutgoing() => _outgoing.Count > 0;
        
        public override void NextTick(ClientId source)
        {
            CurTick.Next();
            var tickMessage = new OutgoingMessage{
                tick = CurTick,
                caller = source,
                callee = ClientId.None,
                rpcId = new RpcId(RpcId.NextTickId),
                target = NetworkId.None,
                protocol = Protocol.Tcp,
                perms = RpcPerms.AnyToAll,
                bytes = new byte[TickMessageLength]
            };
            EncodeNextTick(tickMessage.bytes, source, CurTick);
            AddOutgoing(tickMessage);
        }

        public override void AddIncoming(IncomingMessage m)
        {
            _incoming.Enqueue(m, m.tick);
            if (m.rpcId == RpcId.NextTickId)
                _curTicks++;
            _requireCatchup = _curTicks > _maxTicks;
        }

        public override void AddOutgoing(OutgoingMessage m)
        {
            throw new System.NotImplementedException();
        }

        public override bool TryGetNextIncoming(out IncomingMessage m)
        {
            if (_incoming.TryDequeue(out m))
            {
                if (m.rpcId == RpcId.CurTickId)
                    curReceivingTick = m.tick;
                else if (m.rpcId == RpcId.NextTickId)
                {
                    curReceivingTick = m.tick;
                    if (!_requireCatchup)
                        return false;
                }

                m.tick = curReceivingTick;
                return true;
            }
            return false;
        }

        public override bool TryGetNextOutgoing(out OutgoingMessage m)
        {
            throw new System.NotImplementedException();
        }

        public override void InitBuffer(int tickRate, int latency)
        {
            throw new System.NotImplementedException();
        }
    }
}