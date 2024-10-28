
using System;
using System.Collections;
using System.Collections.Generic;

namespace OwlTree
{
    /// <summary>
    /// A Dictionary wrapper that implements the IEncodable interface.
    /// NetworkDicts have a fixed capacity.
    /// </summary>
    public class NetworkDict<C, K, V> : IEncodable, IVariableLength, IEnumerable<KeyValuePair<K, V>> where C : ICapacity
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
        private bool _keyIsVariable;
        private bool _valueIsVariable;

        public NetworkDict()
        {
            int capacity = ((ICapacity)Activator.CreateInstance(typeof(C))).Capacity();
            if (capacity <= 0)
                throw new ArgumentException("NetworkDict capacity must be greater than 0.");
            Capacity = capacity;

            if (!RpcProtocol.IsEncodableParam(typeof(K)))
            {
                throw new ArgumentException("NetworkDict keys must be an encodable type.");
            }

            if (!RpcProtocol.IsEncodableParam(typeof(V)))
            {
                throw new ArgumentException("NetworkDict values must be an encodable type.");
            }

            _dict = new Dictionary<K, V>(capacity);

            _maxLen = 4 + (Capacity * (RpcProtocol.GetMaxLength(typeof(K)) + RpcProtocol.GetMaxLength(typeof(V))));

            _keyIsVariable = typeof(K) == typeof(IVariableLength);
            _valueIsVariable = typeof(V) == typeof(IVariableLength);
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

        public int ByteLength()
        {
            int total = 4;
            foreach (var elem in this)
            {
                total += RpcProtocol.GetExpectedLength(elem.Key) + RpcProtocol.GetExpectedLength(elem.Value);
            }
            return total;
        }

        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            int count = BitConverter.ToInt32(bytes);
            count = Math.Min(Capacity, count);

            Clear();

            int ind = 4;
            while (count > 0)
            {
                int keyLen = 0;
                if (_keyIsVariable)
                {
                    keyLen = BitConverter.ToInt32(bytes.Slice(ind));
                    ind += 4;
                }
                else
                {
                    keyLen = RpcProtocol.GetMaxLength(typeof(K));
                }
                var nextKey = (K)RpcProtocol.DecodeObject(bytes.Slice(ind, keyLen), ref ind, typeof(K));

                var valLen = 0;
                if (_valueIsVariable)
                {
                    valLen = BitConverter.ToInt32(bytes.Slice(ind + keyLen));
                    ind += 4;
                }
                else
                {
                    valLen = RpcProtocol.GetMaxLength(typeof(V));
                }
                var nextValue = (V)RpcProtocol.DecodeObject(bytes.Slice(ind + keyLen, valLen), ref ind, typeof(V));

                Add(nextKey, nextValue);
                count -= 1;
            }
        }

        public bool InsertBytes(Span<byte> bytes)
        {
            BitConverter.TryWriteBytes(bytes, Count);

            int ind = 4;
            foreach (var elem in this)
            {
                int keyLen = RpcProtocol.GetExpectedLength(elem.Key);
                int valLen = RpcProtocol.GetExpectedLength(elem.Value);

                if (_keyIsVariable)
                {
                    BitConverter.TryWriteBytes(bytes.Slice(ind), keyLen);
                    ind += 4;
                }
                RpcProtocol.InsertBytes(bytes.Slice(ind, keyLen), elem.Key);
                ind += keyLen;

                if (_valueIsVariable)
                {
                    BitConverter.TryWriteBytes(bytes.Slice(ind), valLen);
                    ind += 4;
                }
                RpcProtocol.InsertBytes(bytes.Slice(ind, valLen), elem.Value);
                ind += valLen;
            }

            return true;
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