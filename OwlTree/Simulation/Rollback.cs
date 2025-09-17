using System.Collections.Generic;
using Priority_Queue;

/*
operation:
stores past ticks in a message stack, regulates message consumption to consume 1 tick's messages per execute queue.

Distance between present tick and local tick is based on the travel time of init message.

The authority is initialized immediately, clients are initialized once they receive a CurTick message from the authority.
The CurTick message is sent once the authority adds the new client as a "tick source".

Local tick tries to approximately match with the authority's local tick. After initialization the local
tick and present tick will walk together, keeping a constant distance.

exitTick will usually be 1 ahead of present tick, and is used to know when to stop consuming messages 
for the current execute queue.

newestTick is the latest tick received, which represents the most current tick known to have been simulated
by others in the session.

MaxTicks dictates how many ticks the present simulation can fall behind the session/authority by before performing
a catchup, which means simulating multiple ticks in a single execute queue. This will shift the
present tick up to the newestTick, and if the local tick is behind the new present tick (compensated for latency),
the local tick will be shifted forward, too.

consumed messages will be places in the message stack to be requeued if needed for resimulation.

resimulation is triggered if a new message is received from a tick older that the present tick. When this happens,
the simulation will be rolled back to the new message's tick. Meaning messages from the past stack will popped and requeued.
On the next execute queue, all messages from the new message's old tick to the present exit tick will be consumed.
*/

namespace OwlTree
{
    /// <summary>
    /// Resimulates past ticks when new messages from past ticks are received.
    /// </summary>
    internal class Rollback : SimulationBuffer
    {
        public Rollback(Logger logger, IClientRegistry registry, IReplicator replicator) : base(logger, registry, replicator)
        {

        }

        private SimplePriorityQueue<IncomingMessage, uint> _incoming = new();
        private MessageStack _past;

        // restores the simulation back to this tick
        private void RewindTo(Tick tick)
        {
            foreach (var m in _past.RewindFrom(tick))
                _incoming.Enqueue(m, m.tick);

            _requiresResimulation = true;
            _resimulationStart = true;
            _resimulateFrom = tick;
        }

        private SimplePriorityQueue<OutgoingMessage, uint> _outgoing = new();

        // tracks what ticks other clients are on
        private Dictionary<ClientId, TickPair> _sessionTicks = new();

        // when incoming messages should stop being provided
        private Tick _exitTick = new Tick(0);

        private Tick _resimulateFrom = new Tick(0);
        private bool _requiresResimulation = false;
        private bool _resimulationStart = false;

        private int _maxTicks;
        private int _tickRate;
        private bool _initialized = false;
        private int _latency;

        private Tick _newestTick = new Tick(0);

        protected override void InitBufferInternal(int tickRate, int latency, uint curTick)
        {
            _maxTicks = Replicator.GetSimulationBufferSize();
            PresentTick = new Tick(curTick);
            _exitTick = PresentTick.Next();
            LocalTick = CompensateForLatency(PresentTick, latency, tickRate);
            _past = new MessageStack(Replicator.GetSimulationBufferSize());

            _latency = latency;
            _tickRate = tickRate;
            _initialized = ClientRegistry.GetIsAuthority();

            if (Logger.includes.simulationEvents)
                Logger.Write($"Authority Rollback simulation buffer initialized at local tick {LocalTick} and present tick {PresentTick} based on a latency of {latency}ms.");
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
                    Logger.Write($"Simulation is too far behind. Catching up from tick {PresentTick} to tick {_exitTick}, local tick is now {LocalTick}.");
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

                if (Logger.includes.rpcReceiveEncodings)
                    Logger.Write("RECEIVING:\n" + TickEncodingSummary(m.rpcId, m.caller, m.callee, m.tick, (long)m.args[0]));

                _latency = Timestamp.MillisecondsSince((long)m.args[0]);
                LocalTick = CompensateForLatency(m.tick, _latency, _tickRate);
                PresentTick = m.tick;
                _exitTick = PresentTick.Next();
                _initialized = true;

                if (Logger.includes.simulationEvents)
                    Logger.Write($"Received session tick value from authority of {m.tick}. Compensated for latency, local tick is now {LocalTick}.");

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

            // if resimulation is required
            if (m.tick < PresentTick && (!_requiresResimulation || m.tick < _resimulateFrom))
            {
                RewindTo(m.tick);
                if (Logger.includes.simulationEvents)
                    Logger.Write($"Received message from past tick {m.tick}, resimulating {PresentTick - m.tick} tick(s) on next ExecuteQueue().");
            }

            _incoming.Enqueue(m, m.tick);
        }

        protected override bool TryGetNextIncomingInternal(out IncomingMessage m)
        {

            if (_incoming.TryFirst(out m))
            {
                PresentTick = m.tick;

                if (_resimulationStart)
                {
                    OnResimulation?.Invoke(PresentTick);
                    _resimulationStart = false;
                }

                if (m.tick >= _exitTick)
                {
                    if (_requiresResimulation && Logger.includes.simulationEvents)
                        Logger.Write($"Resimulation complete, resimulated from tick {_resimulateFrom} to {PresentTick.Prev()}.");
                    _requiresResimulation = false;
                    return false;
                }
                _incoming.Dequeue();
                if (m.rpcId != RpcId.PingRequestId && !m.rpcId.IsClientEvent() && !m.rpcId.IsObjectEvent())
                    _past?.Push(m);
                return true;
            }
            PresentTick = _exitTick;
            _requiresResimulation = false;
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
                _sessionTicks.Add(client, new TickPair(PresentTick, PresentTick));
            }
        }

        protected override void RemoveTickSourceInternal(ClientId client)
        {
            _sessionTicks.Remove(client);
        }
    }
}