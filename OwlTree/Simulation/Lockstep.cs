using System.Collections.Generic;
using System.Linq;
using Priority_Queue;

/*
operation:
Does not store past ticks, regulates message consumption to consume 1 tick's messages per execute queue,
and the next tick can only be consumed if it is "complete" meaning all messages for that tick from all 
tick sources have been received.

A tick source is known to have completed a tick once the NextTick message is received from them.
A tick is known to be complete once all NextTick messages have been received.

Distance between present tick and local tick is based on the travel time of init message.

The authority is initialized immediately, clients are initialized once they receive a CurTick message from the authority.
The CurTick message is sent once the authority adds the new client as a "tick source".

Local tick tries to approximately match with authority's local tick. After initialization the 
local tick will walk separately from present tick, as present tick walks based on the pace ticks are completed at.
Local tick walks at the constant pace execute queue is invoked at, which is assumed to be the tickrate.

exitTick will always be 1 ahead of present tick, and is used to know when to stop consuming messages for the 
current execute queue.

lastCompleteTick is the newest tick that has been completed, meaning it is farthest the present is allowed to go before halting.

MaxTicks dictates how many ticks the present tick can fall behind the lastCompleteTick by before performing a catchup,
which means simulating multiple ticks in a single execute queue. This will shift the present tick up to the lastCompleteTick,
and if the local tick is behind the new present tick (compensated for latency), the local tick will be shifted
forward, too.
*/

namespace OwlTree
{
    /// <summary>
    /// Messages are sorted by tick, and stop between each tick.
    /// Ticks are not executed until all connections have reported their tick.
    /// </summary>
    internal class Lockstep : SimulationBuffer
    {
        private SimplePriorityQueue<IncomingMessage, uint> _incoming = new();
        private SimplePriorityQueue<OutgoingMessage, uint> _outgoing = new();

        // tracks what ticks other clients are on
        private Dictionary<ClientId, TickPair> _sessionTicks = new();

        // when incoming messages should stop being provided
        private Tick _exitTick = new Tick(0);
        // the newest tick that all clients have completed
        private Tick _lastCompleteTick = new Tick(0);

        private int _maxTicks;
        private int _tickRate;
        private int _latency;

        private bool _initialized = false;

        public Lockstep(Logger logger, IClientRegistry registry, IReplicator replicator) : base(logger, registry, replicator)
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
                Logger.Write($"Authority Lockstep simulation buffer initialized at local tick {LocalTick} and present tick {PresentTick} based on a latency of {latency}ms.");
        }

        protected override void NextTickInternal()
        {
            LocalTick = LocalTick.Next();
            _exitTick = PresentTick.Next();

            if (_lastCompleteTick > _exitTick && _lastCompleteTick - _exitTick > _maxTicks)
            {
                _exitTick = _lastCompleteTick.Prev();
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

                if (Logger.includes.rpcReceiveEncodings)
                    Logger.Write("RECEIVING:\n" + TickEncodingSummary(m.rpcId, m.caller, m.callee, m.tick, (long)m.args[0]));

                _latency = Timestamp.MillisecondsSince((long)m.args[0]);
                LocalTick = CompensateForLatency(m.tick, _latency, _tickRate);
                PresentTick = m.tick;
                _exitTick = PresentTick.Next();
                _initialized = true;

                if (Logger.includes.simulationEvents)
                    Logger.Write($"Client Lockstep simulation buffer initialized. Received session tick value from authority of {m.tick}. Compensated for latency of {_latency}ms, local tick is now {LocalTick}.");

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
                var prevTick = _sessionTicks[m.caller].Select(m.protocol);

                _sessionTicks[m.caller].Update(m.protocol, m.tick);

                // if the caller was the last client that needed to move off of the prev tick
                // the prev tick is now complete and can be simulated
                if (prevTick < _sessionTicks.Min(p => p.Value.Min().Value))
                {
                    _lastCompleteTick = prevTick;

                    if (Logger.includes.simulationEvents)
                        Logger.Write($"Received all messages for tick {_lastCompleteTick}.");
                }

                return;
            }

            m.tick = _sessionTicks[m.caller].Select(m.protocol);
            _incoming.Enqueue(m, m.tick);
        }

        protected override bool TryGetNextIncomingInternal(out IncomingMessage m)
        {
            // wait for the present tick to be ready before simulating
            if (_sessionTicks.Count > 0 && PresentTick > _lastCompleteTick &&
                (_incoming.Count == 0 || _incoming.First.rpcId != RpcId.PingRequestId))
            {
                m = new IncomingMessage();
                return false;
            }

            if (_incoming.TryFirst(out m))
            {
                PresentTick = m.tick;
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
                _sessionTicks.Add(client, new TickPair(PresentTick, PresentTick));
            }
        }

        protected override void RemoveTickSourceInternal(ClientId client)
        {
            _sessionTicks.Remove(client);
        }
    }
}