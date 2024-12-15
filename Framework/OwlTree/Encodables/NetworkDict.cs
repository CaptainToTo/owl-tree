
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace OwlTree
{
    /// <summary>
    /// A Dictionary wrapper that implements the IEncodable interface.
    /// NetworkDicts have a fixed capacity.
    /// </summary>
    public class NetworkDict<C, K, V> : IEncodable, IVariableLength, IEnumerable<KeyValuePair<K, V>> where C : ICapacity, new() where K : new() where V : new()
    {
        private Dictionary<K, V> _dict;

        /// <summary>
        /// The max number of pairs this dictionary can hold. Defined by Capacity type.
        /// </summary>
        public int Capacity { get; private set; }
        /// <summary>
        /// The number of pairs currently in this dictionary.
        /// </summary>
        public int Count { get { return _dict.Count; } }

        public bool IsFull { get { return Count == Capacity; } }
        public bool IsEmpty { get { return Count == 0; } }

        private int _maxLen;

        public NetworkDict()
        {
            int capacity = new C().Capacity();
            if (capacity <= 0)
                throw new ArgumentException("NetworkDict capacity must be greater than 0.");
            Capacity = capacity;

            if (!RpcEncoding.IsEncodable<K>())
            {
                throw new ArgumentException("NetworkDict keys must be an encodable type.");
            }

            if (!RpcEncoding.IsEncodable<V>())
            {
                throw new ArgumentException("NetworkDict values must be an encodable type.");
            }

            _dict = new Dictionary<K, V>(capacity);

            _maxLen = 4 + (Capacity * (RpcEncoding.GetMaxLength(typeof(K)) + RpcEncoding.GetMaxLength(typeof(V))));
        }

        /// <summary>
        /// Adds a new key-value pair to the dictionary.
        /// </summary>
        public void Add(K key, V value)
        {
            if (IsFull)
                throw new InvalidOperationException("Cannot add to full NetworkDict.");
            _dict.Add(key, value);
        }

        /// <summary>
        /// Returns true if this dictionary contains the given key.
        /// </summary>
        public bool ContainsKey(K key)
        {
            return _dict.ContainsKey(key);
        }

        /// <summary>
        /// Returns true if this dictionary contains the given value.
        /// </summary>
        public bool ContainsValue(V value)
        {
            return _dict.ContainsValue(value);
        }

        /// <summary>
        /// Removes the key-value pair that has the given key. Returns true if pair was successfully removed.
        /// </summary>
        public bool Remove(K key)
        {
            return _dict.Remove(key);
        }

        public V this[K k]
        {
            get => _dict[k];
            set => _dict[k] = value;
        }

        /// <summary>
        /// Returns the value associated with the given key.
        /// </summary>
        public V Get(K key)
        {
            return _dict[key];
        }

        /// <summary>
        /// Tries to get the value associated with the given key.
        /// Returns true if the value was successfully retrieved.
        /// </summary>
        public bool TryGetValue(K key, out V value)
        {
            return _dict.TryGetValue(key, out value);
        }

        /// <summary>
        /// Remove all pairs from this dictionary.
        /// </summary>
        public void Clear()
        {
            _dict.Clear();
        }

        public Dictionary<K, V>.Enumerator GetEnumerator()
        {
            return _dict.GetEnumerator();
        }

        IEnumerator<KeyValuePair<K, V>> IEnumerable<KeyValuePair<K, V>>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerable<K> Keys { get => _dict.Select(p => p.Key); }

        public IEnumerable<V> Values { get => _dict.Select(p => p.Value); }

        public int ByteLength()
        {
            int total = 4;
            foreach (var elem in this)
            {
                total += RpcEncoding.GetExpectedLength(elem.Key) + RpcEncoding.GetExpectedLength(elem.Value);
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
                var nextKey = (K)RpcEncoding.DecodeObject(bytes.Slice(ind), typeof(K), out len);
                ind += len;
                var nextValue = (V)RpcEncoding.DecodeObject(bytes.Slice(ind), typeof(V), out len);
                ind += len;

                Add(nextKey, nextValue);
                count -= 1;
            }
        }

        public void InsertBytes(Span<byte> bytes)
        {
            BitConverter.TryWriteBytes(bytes, Count);

            int ind = 4;
            foreach (var elem in this)
            {
                int keyLen = RpcEncoding.GetExpectedLength(elem.Key);
                int valLen = RpcEncoding.GetExpectedLength(elem.Value);

                RpcEncoding.InsertBytes(bytes.Slice(ind, keyLen), elem.Key);
                ind += keyLen;

                RpcEncoding.InsertBytes(bytes.Slice(ind, valLen), elem.Value);
                ind += valLen;
            }
        }

        public int MaxLength()
        {
            return _maxLen;
        }

        public override string ToString()
        {
            return "<NetworkDict<" + typeof(K).ToString() + ", " + typeof(V).ToString() + ">; Capacity: " + Capacity + "; Count: " + Count + ">";
        }
    }

}