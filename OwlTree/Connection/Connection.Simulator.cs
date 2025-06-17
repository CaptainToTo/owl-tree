namespace OwlTree
{
    public partial class Connection : ISimulator
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
        public Tick LocalTick => _simBuffer.LocalTick();
        /// <summary>
        /// The current simulation tick of received RPCs currently being run. This will usually be behind
        /// LocalTick. If simulation management is disabled, this will always be 0.
        /// </summary>
        public Tick PresentTick => _simBuffer.PresentTick();
        internal Tick GetPresentTick() => _simBuffer.PresentTick();
        Tick ISimulator.GetPresentTick() => GetPresentTick();
        /// <summary>
        /// The expected rate at which <c>ExecuteQueue()</c> should be called in milliseconds.
        /// </summary>
        public int TickRate { get; private set; }
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

        internal void AddSimulated(ISimulated simulated) => OnResimulation += simulated.OnResimulation;
        void ISimulator.AddSimulated(ISimulated simulated) => AddSimulated(simulated);
        internal void RemoveSimulated(ISimulated simulated) => OnResimulation -= simulated.OnResimulation;
        void ISimulator.RemoveSimulated(ISimulated simulated) => RemoveSimulated(simulated);
    }
}