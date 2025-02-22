namespace OwlTree
{
    public class Snapshot : SimulationBuffer
    {
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

        public override void AddMessage(Message m)
        {
            buffer.Enqueue(m, m.tick);
            if (m.rpcId == RpcId.NextTickId)
                _curTicks++;
            _requireCatchup = _curTicks > _maxTicks;
        }

        public override bool GetNextMessage(out Message m)
        {
            if (buffer.TryDequeue(out m))
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
    }
}