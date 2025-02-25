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

        protected override bool HasOutgoingInternal() => _outgoing.Count > 0;

        protected override void NextTickInternal()
        {
            // message queue doesn't track simulation ticks
        }

        protected override void AddIncomingInternal(IncomingMessage m)
        {
            _incoming.Enqueue(m);
        }

        protected override void AddOutgoingInternal(OutgoingMessage m)
        {
            _outgoing.Enqueue(m);
        }

        protected override bool TryGetNextIncomingInternal(out IncomingMessage m)
        {
            return _incoming.TryDequeue(out m);
        }

        protected override bool TryGetNextOutgoingInternal(out OutgoingMessage m)
        {
            return _outgoing.TryDequeue(out m);
        }

        protected override void InitBufferInternal(int tickRate, int latency, uint curTick, ClientId localId, bool isAuthority)
        {
            // message queue does not maintain simulation buffer
        }

        protected override void AddTickSourceInternal(ClientId client)
        {
            // message queue does not maintain simulation buffer
        }

        protected override void RemoveTickSourceInternal(ClientId client)
        {
            // message queue does not maintain simulation buffer
        }

        protected override void UpdateAuthorityInternal(bool isAuthority)
        {
            // message queue does not maintain simulation buffer
        }
    }
}