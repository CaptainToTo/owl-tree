using System;

namespace OwlTree
{
    /// <summary>
    /// Use to create replicated properties on NetworkObjects. These will store values from
    /// previous ticks, and provide the value that matches the connection's present tick.
    /// </summary>
    public class Synced<T> : IReplicated where T : IReplicatable<T>
    {
        /// <summary>
        /// The underlying value this is managing.
        /// </summary>
        public T Value { 
            get => ValueAt(_replicator.GetPresentTick());
            set => SetAt(_replicator.GetPresentTick(), value);
        }

        /// <summary>
        /// Gets the value of the property at a given tick.
        /// </summary>
        public T ValueAt(Tick t)
        {
            if (_replicator == null)
                throw new InvalidOperationException("Cannot access a synchronized value until it has been initialized.");

            // if newest value was set in a past tick, meaning requested tick doesn't have a value
            if (_newestTick < t)
                return _values[_newestTick % _values.Length];
            
            // if requested tick is too old and no longer exists
            else if (_newestTick - t > _values.Length)
                return _values[OldestTick() % _values.Length];
            
            // if requested tick has an existing value
            return _values[t % _values.Length];
        }

        private Tick OldestTick() => new Tick(Math.Max(_newestTick - ((uint)_values.Length - 1), _firstTick));

        private void SetAt(Tick t, T val)
        {
            if (_replicator == null)
                throw new InvalidOperationException("Cannot set a synchronized value until it has been initialized.");

            if (OldestTick() > t)
                return;

            if (_newestTick < t)
            {
                for (uint i = _newestTick; i < t; i++)
                    _values[i % _values.Length] = _values[_newestTick % _values.Length];
                _newestTick = t;
            }

            _values[t % _values.Length] = val;
        }

        private T[] _values;
        private IReplicator _replicator;
        private Tick _newestTick;
        private Tick _firstTick;

        internal Synced()
        {
            _values = null;
            _replicator = null;
        }

        ~Synced()
        {
            if (_replicator != null)
                _replicator.RemoveReplicated(this);
        }

        internal int ByteLength(Tick prev, Tick cur)
        {
            return ValueAt(cur).DeltaLength(ValueAt(prev));
        }

        internal bool HasChanged(Tick prev, Tick cur)
        {
            return ValueAt(prev).Equals(ValueAt(cur));
        }

        internal void FromBytes(ReadOnlySpan<byte> bytes, Tick prev, Tick cur)
        {
            SetAt(cur, ValueAt(prev).FromDelta(bytes));
        }

        internal void InsertBytes(Span<byte> bytes, Tick prev, Tick cur)
        {
            ValueAt(cur).InsertDelta(bytes, ValueAt(prev));
        }

        internal void Initialize(int bufferSize, IReplicator simulator)
        {
            _values = new T[bufferSize];
            _replicator = simulator;
            _replicator.AddReplicated(this);
            _newestTick = _replicator.GetPresentTick();
            _firstTick = _replicator.GetPresentTick();
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

        void IReplicated.Initialize(int bufferSize, IReplicator replicator) => Initialize(bufferSize, replicator);

        bool IReplicated.HasChanged(Tick prev, Tick cur) => HasChanged(prev, cur);
        int IReplicated.ByteLength(Tick prev, Tick cur) => ByteLength(prev, cur);
        void IReplicated.InsertBytes(Span<byte> bytes, Tick prev, Tick cur) => InsertBytes(bytes, prev, cur);
        void IReplicated.FromBytes(ReadOnlySpan<byte> bytes, Tick prev, Tick cur) => FromBytes(bytes, prev, cur);

        void IReplicated.OnResimulation(Tick t) => OnResimulation(t);

        public static implicit operator T(Synced<T> a) => a.Value;
    }
}