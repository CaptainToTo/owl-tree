using System;
using System.Collections;
using System.Collections.Generic;

namespace OwlTree
{
    /// <summary>
    /// A variable length list of IEncodable objects. NetworkLists have a fixed capacity.
    /// </summary>
    public class NetworkList<C, T> : IEncodable, IVariableLength, IEnumerable<T> where C : ICapacity, new() where T : new()
    {
        private List<T> _list;

        /// <summary>
        /// The max number of elements this list can hold. Defined by Capacity type.
        /// </summary>
        public int Capacity { get; private set; }
        /// <summary>
        /// The number of elements currently in this list.
        /// </summary>
        public int Count { get { return _list.Count; } }

        public bool IsFull { get { return Count == Capacity; } }
        public bool IsEmpty { get { return Count == 0; } }

        private int _maxLen;

        public NetworkList()
        {
            int capacity = new C().Capacity();
            if (capacity <= 0)
                throw new ArgumentException("NetworkList capacity must be greater than 0.");
            Capacity = capacity;

            if (!RpcEncoding.IsEncodable<T>())
            {
                throw new ArgumentException("NetworkList must have an encodable type.");
            }

            _list = new List<T>(capacity);

            _maxLen = 4 + (Capacity * RpcEncoding.GetMaxLength(typeof(T)));
        }

        /// <summary>
        /// Adds a new element at the end of the list.
        /// </summary>
        public void Add(T elem)
        {
            if (IsFull)
                throw new InvalidOperationException("Cannot add to full NetworkList.");
            _list.Add(elem);
        }

        /// <summary>
        /// Insert a new element at ind.
        /// </summary>
        public void Insert(int ind, T elem)
        {
            _list.Insert(ind, elem);
        }

        /// <summary>
        /// Find the index of the given element. Returns -1 if not found.
        /// </summary>
        public int IndexOf(T elem)
        {
            return _list.IndexOf(elem);
        }

        /// <summary>
        /// Removes the given element from the list. Returns true if the element was removed.
        /// </summary>
        public bool Remove(T elem)
        {
            return _list.Remove(elem);
        }

        public T this[int i]
        {
            get => _list[i];
            set => _list[i] = value;
        }

        /// <summary>
        /// Removes all elements from the list.
        /// </summary>
        public void Clear()
        {
            _list.Clear();
        }

        /// <summary>
        /// Remove the element at the given index.
        /// </summary>
        public void RemoveAt(int ind)
        {
            _list.RemoveAt(ind);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        public int ByteLength()
        {
            int total = 4;
            foreach (var elem in this)
            {
                total +=  RpcEncoding.GetExpectedLength(elem);
            }
            return total;
        }

        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            int count = BitConverter.ToInt32(bytes);
            count = Math.Min(Capacity, count);

            Clear();

            int ind = 4;
            int len = 0;
            while (count > 0)
            {
                var nextElem = (T)RpcEncoding.DecodeObject(bytes.Slice(ind), typeof(T), out len);
                ind += len;
                Add(nextElem);
                count -= 1;
            }
        }

        public void InsertBytes(Span<byte> bytes)
        {
            BitConverter.TryWriteBytes(bytes, Count);

            int ind = 4;
            foreach (var elem in this)
            {
                int len = RpcEncoding.GetExpectedLength(elem);
                RpcEncoding.InsertBytes(bytes.Slice(ind, len), elem);
                ind += len;
            }
        }

        public int MaxLength()
        {
            return _maxLen;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override string ToString()
        {
            return "<NetworkList<" + typeof(T).ToString() + ">; Capacity: " + Capacity + "; Count: " + Count + ">";
        }
    }
}