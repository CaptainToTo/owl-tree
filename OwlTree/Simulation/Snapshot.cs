using System.Collections.Generic;
using Priority_Queue;

/*
operation:
Does not store past ticks, only regulates message consumption to consume 1 tick's messages per execute queue.
Distance between present tick and local tick is based on travel time of init message.

The authority is initialized immediately, clients are initialized once they receive a CurTick message from the authority.
The CurTick message is sent once the authority adds the new client as a "tick source".

Local tick tries to approximately match with authority's local tick. After initialization the 
local tick and present tick will walk together, keeping a consistent distance.

exitTick will always be 1 ahead of present tick, and is used to know when to stop consuming messages for the 
current execute queue.

newestTick is the latest tick received, which represents the most current tick known to have been simulated
by others in the session.

MaxTicks dictates how many ticks the present simulation can fall behind the session/authority by before performing
a catchup, which means simulating multiple ticks in a single execute queue. This will shift the
present tick up to the newestTick, and if the local tick is behind the new present tick (compensated for latency),
the local tick will be shifted forward, too.

messages received that belong to an older tick than the present tick will be consumed with the next execute queue,
meaning consumption can occur out of order.
*/

namespace OwlTree
{
    /// <summary>
    /// Messages are sorted by tick, and stop between each tick.
    /// Past messages are lost, and no rollback or synchronization occurs.
    /// </summary>
    internal class Snapshot : SimulationBuffer
    {
        private SimplePriorityQueue<IncomingMessage, uint> _incoming = new();
        private SimplePriorityQueue<OutgoingMessage, uint> _outgoing = new();

        // tracks what ticks other clients are on
        private Dictionary<ClientId, TickPair> _sessionTicks = new();

        // when incoming messages should stop being provided
        private Tick _exitTick = new Tick(0);

        private int _maxTicks;
        private bool _initialized = false;
        private int _tickRate;
        private int _latency;

        private Tick _newestTick = new Tick(0);

        public Snapshot(Logger logger, IClientRegistry registry, IReplicator replicator) : base(logger, registry, replicator)
        {
        }


        protected override void InitBufferInternal(int tickRate, int latency, uint curTick)
        {
            _maxTicks = Replicator.GetSimulationBufferSize();
            PresentTick = new Tick(curTick);
            _exitTick = PresentTick.Next();
            LocalTick = CompensateForLatency(PresentTick, latency, tickRate);

            _latency = latency;
            _tickRate = tickRate;
            _initialized = ClientRegistry.GetIsAuthority();

            if (Logger.includes.simulationEvents && _initialized)
                Logger.Write($"Authority Snapshot simulation buffer initialized at local tick {LocalTick} and present tick {PresentTick} based on a latency of {latency}ms.");
        }

        protected override void NextTickInternal()
        {
            LocalTick = LocalTick.Next();
            _exitTick = PresentTick.Next();
            if (_newestTick > _exitTick && _newestTick - _exitTick > _maxTicks)
            {
                _exitTick = _newestTick.Prev();
                var comp = CompensateForLatency(_exitTick, _latency, _tickRate);
                if (comp > LocalTick)
                    LocalTick = comp;
                
                if (Logger.includes.simulationEvents)
                    Logger.Write($"Simulation is too far behind. Catching up from tick {PresentTick} to tick {_exitTick}, Local tick is now {LocalTick}.");
            }

            if (!_initialized) return;

            SendNextTick(_outgoing);
        }

        protected override bool HasOutgoingInternal() => _outgoing.Count > 0;

        protected override void AddOutgoingInternal(OutgoingMessage m)
        {
            m.tick = _initialized ? LocalTick : new Tick(0);
            _outgoing.Enqueue(m, m.tick);
        }

        protected override bool TryGetNextOutgoingInternal(out OutgoingMessage m)
        {
            if (!_initialized)
            {
                m = new OutgoingMessage();
                return false;
            }

            return _outgoing.TryDequeue(out m);
        }

        protected override void AddIncomingInternal(IncomingMessage m)
        {
            if (m.rpcId == RpcId.PingRequestId)
            {
                _incoming.Enqueue(m, 0);
                return;
            }

            if (!_sessionTicks.ContainsKey(m.caller))
            {
                m.tick = LocalTick;
                _incoming.Enqueue(m, m.tick);
                return;
            }

            // initialize non-authority connections
            if (m.rpcId == RpcId.CurTickId)
            {
                if (m.caller != ClientRegistry.GetAuthority()) return;

                if (Logger.includes.simulationEncodings)
                    Logger.Write("RECEIVING:\n" + TickEncodingSummary(m.rpcId, m.caller, m.callee, m.tick, (long)m.args[0]));

                _latency = Timestamp.MillisecondsSince((long)m.args[0]);
                LocalTick = CompensateForLatency(m.tick, _latency, _tickRate);
                PresentTick = m.tick;
                _exitTick = PresentTick.Next();
                _initialized = true;

                if (Logger.includes.simulationEvents)
                    Logger.Write($"Client Snapshot simulation buffer initialized. Received session tick value from authority of {m.tick}. Compensated for latency of {_latency}ms, local tick is now {LocalTick}.");

                SendNextTick(_outgoing);

                // update tick of any outgoing messages that were enqueued before initialization
                while (_outgoing.TryFirst(out var outgoing) && outgoing.tick == 0)
                {
                    _outgoing.Dequeue();
                    outgoing.tick = LocalTick;
                    _outgoing.Enqueue(outgoing, outgoing.tick);
                }

                return;
            }

            // a client moved to a new tick
            if (m.rpcId == RpcId.NextTickId)
            {
                _sessionTicks[m.caller].Update(m.protocol, m.tick);

                if (m.tick > _newestTick)
                    _newestTick = m.tick;

                return;
            }

            m.tick = _sessionTicks[m.caller].Select(m.protocol);
            _incoming.Enqueue(m, m.tick);
        }

        protected override bool TryGetNextIncomingInternal(out IncomingMessage m)
        {
            if (_incoming.TryFirst(out m))
            {
                PresentTick = m.tick;
                // continue providing messages until the present tick is complete
                if (m.tick >= _exitTick)
                {
                    return false;
                }
                _incoming.Dequeue();
                return true;
            }
            PresentTick = _exitTick;
            return false;
        }

        protected override void AddTickSourceInternal(ClientId client)
        {
            if (ClientRegistry.GetIsAuthority())
            {
                _sessionTicks.Add(client, new TickPair(LocalTick, LocalTick));

                if (Logger.includes.simulationEvents)
                    Logger.Write($"Sending session tick {LocalTick} to {client}.");

                SendCurTick(_outgoing, client);
            }
            else
            {
                // set to present tick since it's the oldest known tick
                _sessionTicks.Add(client, new TickPair(PresentTick, PresentTick));
            }
        }

        protected override void RemoveTickSourceInternal(ClientId client)
        {
            _sessionTicks.Remove(client);
        }
    }
}