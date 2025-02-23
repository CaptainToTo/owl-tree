using System.Collections.Concurrent;

namespace OwlTree
{
    /// <summary>
    /// Simple message queue. Does not implement any simulation management.
    /// </summary>
    public class MessageQueue : SimulationBuffer
    {
        private ConcurrentQueue<IncomingMessage> _incoming = new();
        private ConcurrentQueue<OutgoingMessage> _outgoing = new();

        public override bool HasOutgoing() => _outgoing.Count > 0;

        public override void NextTick(ClientId source)
        {
            // message queue doesn't track simulation ticks
        }

        public override void AddIncoming(IncomingMessage m)
        {
            _incoming.Enqueue(m);
        }

        public override void AddOutgoing(OutgoingMessage m)
        {
            _outgoing.Enqueue(m);
        }

        public override bool TryGetNextIncoming(out IncomingMessage m)
        {
            return _incoming.TryDequeue(out m);
        }

        public override bool TryGetNextOutgoing(out OutgoingMessage m)
        {
            return _outgoing.TryDequeue(out m);
        }

        public override void InitBuffer(int tickRate, int latency)
        {
            // message queue does not maintain simulation buffer
        }
    }
}