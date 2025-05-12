
using System;

namespace OwlTree
{
    internal interface ISimulated 
    {
        internal void Initialize(int bufferSize, ISimulator simulator);
        internal void OnResimulation(Tick t);
    }

    internal interface ISimulator
    {
        internal Tick GetPresentTick();
        internal void AddSimulated(ISimulated simulated);
        internal void RemoveSimulated(ISimulated simulated);
    }

    /// <summary>
    /// Use to create simulated properties on NetworkObjects. These will store values from
    /// previous ticks, and provide the value that matches the connection's present tick.
    /// </summary>
    public class Simulated<T> : ISimulated
    {
        /// <summary>
        /// The underlying value this is managing.
        /// </summary>
        public T Value { 
            get {
                if (_simulator == null)
                    throw new InvalidOperationException("Cannot access a simulated value until it has been initialized.");

                var present = _simulator.GetPresentTick();;
                if (_newestTick < present)
                    return _values[_newestTick % _values.Length];
                else if (_newestTick - present > _values.Length)
                    return _values[OldestTick() % _values.Length];
                return _values[present % _values.Length];
            }
            set {
                if (_simulator == null)
                    throw new InvalidOperationException("Cannot set a simulated value until it has been initialized.");

                var present = _simulator.GetPresentTick();
                if (_newestTick < present)
                {
                    for (uint i = _newestTick; i < present; i++)
                        _values[i % _values.Length] = _values[_newestTick % _values.Length];
                    _newestTick = present;
                }

                _values[present % _values.Length] = value;
            }
        }

        /// <summary>
        /// Gets the value of the property at a given tick.
        /// </summary>
        public T ValueAt(Tick t)
        {
            if (_simulator == null)
                throw new InvalidOperationException("Cannot access a simulated value until it has been initialized.");

            if (_newestTick < t)
                return _values[_newestTick % _values.Length];
            else if (_newestTick - t > _values.Length)
                return _values[OldestTick() % _values.Length];
            return _values[t % _values.Length];
        }

        private Tick OldestTick() => new Tick(Math.Max(_newestTick - ((uint)_values.Length - 1), _firstTick));

        private T[] _values;
        private ISimulator _simulator;
        private Tick _newestTick;
        private Tick _firstTick;

        internal Simulated()
        {
            _values = null;
            _simulator = null;
        }

        ~Simulated()
        {
            if (_simulator != null)
                _simulator.RemoveSimulated(this);
        }

        internal void Initialize(int bufferSize, ISimulator simulator)
        {
            _values = new T[bufferSize];
            _simulator = simulator;
            _simulator.AddSimulated(this);
            _newestTick = _simulator.GetPresentTick();
            _firstTick = _simulator.GetPresentTick();
        }

        internal void OnResimulation(Tick rewindTo)
        {
            if (_newestTick < rewindTo.Prev())
                return;
            else if (_newestTick - rewindTo.Prev() > _values.Length)
                _newestTick = OldestTick();
            else
                _newestTick = rewindTo.Prev();
        }

        void ISimulated.Initialize(int bufferSize, ISimulator simulator)
        {
            Initialize(bufferSize, simulator);
        }

        void ISimulated.OnResimulation(Tick t)
        {
            OnResimulation(t);
        }

        public static implicit operator T(Simulated<T> a) => a.Value;
    }
}