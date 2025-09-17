namespace OwlTree
{
    public partial class Connection : IReplicator
    {
        private SimulationBuffer _simBuffer;

        /// <summary>
        /// The simulation system this session is using.
        /// </summary>
        public SimulationSystem SimulationSystem {get; private set; }

        /// <summary>
        /// The current simulation tick assigned to RPCs sent from this connection. 
        /// If simulation management is disabled, this will always be 0.
        /// </summary>
        public Tick LocalTick => _simBuffer.GetLocalTick();
        internal Tick GetLocalTick() => _simBuffer.GetLocalTick();
        Tick IReplicator.GetLocalTick() => GetLocalTick();
        /// <summary>
        /// The current simulation tick of received RPCs currently being run. This will usually be behind
        /// LocalTick. If simulation management is disabled, this will always be 0.
        /// </summary>
        public Tick PresentTick => _simBuffer.GetPresentTick();
        internal Tick GetPresentTick() => _simBuffer.GetPresentTick();
        Tick IReplicator.GetPresentTick() => GetPresentTick();
        /// <summary>
        /// The expected rate at which <c>ExecuteQueue()</c> should be called in milliseconds.
        /// </summary>
        public int TickRate { get; private set; }
        int IReplicator.GetTickRate() => TickRate;
        /// <summary>
        /// The number of past ticks that will be stored. If no simulation system is being used, this will be 1.
        /// </summary>
        public int SimulationBufferSize { get; private set; }
        int IReplicator.GetSimulationBufferSize() => SimulationBufferSize;
        /// <summary>
        /// Invoked when the simulation control system triggers a resimulation. Provides the tick
        /// that will be resimulated from.
        /// </summary>
        public event Tick.Delegate OnResimulation;

        /// <summary>
        /// Returns the number of milliseconds between two ticks.
        /// The first tick must be older than the second, otherwise 0 is returned.
        /// </summary>
        public int TimeBetween(Tick a, Tick b)
        {
            if (a >= b) return 0;
            return (int)((b - a) * TickRate);
        }

        /// <summary>
        /// Returns the number of seconds between two ticks as a float.
        /// The first tick must be older than the second, otherwise 0 is returned.
        /// </summary>
        public float SecondsBetween(Tick a, Tick b)
        {
            return TimeBetween(a, b) / 1000f;
        }

        internal void AddReplicated(IReplicated replicated) => OnResimulation += replicated.OnResimulation;
        void IReplicator.AddReplicated(IReplicated replicated) => AddReplicated(replicated);
        internal void RemoveReplicated(IReplicated replicated) => OnResimulation -= replicated.OnResimulation;
        void IReplicator.RemoveReplicated(IReplicated replicated) => RemoveReplicated(replicated);
    }
}