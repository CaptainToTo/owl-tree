
using System;

namespace OwlTree
{
    internal interface IReplicated
    {
        internal void Initialize(int bufferSize, IReplicator replicator);
        internal void OnResimulation(Tick t);

        internal bool HasChanged(Tick prev, Tick cur);
        internal int ByteLength(Tick prev, Tick cur);
        internal void InsertBytes(Span<byte> bytes, Tick prev, Tick cur);
        internal void FromBytes(ReadOnlySpan<byte> bytes, Tick prev, Tick cur);
    }

    internal interface IReplicator
    {
        internal Tick GetLocalTick();
        internal Tick GetPresentTick();
        internal int GetTickRate();
        internal int GetSimulationBufferSize();
        internal void AddReplicated(IReplicated r);
        internal void RemoveReplicated(IReplicated r);
    }

    /// <summary>
    /// Implement to make a struct a valid replication target.
    /// If only IReplicatable is implemented, then the struct should take a fixed number of bytes to encode.
    /// To allow an encoding with variable length, also implement IVariableLength.
    /// </summary>
    public interface IReplicatable<T>
    {
        /// <summary>
        /// Returns the number of bytes it will take to encode the replication delta. The value returned
        /// will be used to allocate the span provided to <c>InsertDelta()</c>.
        /// </summary>
        public int DeltaLength(T prev);

        /// <summary>
        /// Inserts a byte array representation of this object into bytes.
        /// The previous state of this object is provided for finding deltas.
        /// </summary>
        public void InsertDelta(Span<byte> bytes, T prev);

        /// <summary>
        /// Returns an updated version of this object from the provided delta encoding.
        /// Avoid changing this existing object, instead create a new copy and return that.
        /// </summary>
        public T FromDelta(ReadOnlySpan<byte> bytes);
    }
}