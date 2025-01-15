using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace OwlTree
{
    /// <summary>
    /// Manages object maps identified by key-value type pairs.
    /// </summary>
    public class GenericObjectMaps
    {
        // key-value types used as keys to identify maps
        private struct Pair
        {
            public Type k;
            public Type v;

            public Pair(Type k, Type v)
            {
                this.k = k;
                this.v = v;
            }

            public static bool operator ==(Pair a, Pair b)
            {
                return a.k == b.k && a.v == b.v;
            }

            public static bool operator !=(Pair a, Pair b)
            {
                return a.k != b.k || a.v != b.v;
            }

            public override bool Equals(object obj) => obj != null && obj.GetType() == typeof(Pair) && (Pair)obj == this;
            public override int GetHashCode() => base.GetHashCode();
        }

        private Dictionary<Pair, IDictionary> _objectMaps = new();

        /// <summary>
        /// Creates a new map that uses the given key and value types.
        /// </summary>
        public void AddMap<K, V>()
        {
            var pair = new Pair(typeof(K), typeof(V));
            if (!_objectMaps.ContainsKey(pair))
                _objectMaps.Add(pair, new Dictionary<K, V>());
        }

        /// <summary>
        /// Add a new key-value pair to the map with the matching type pair.
        /// </summary>
        public void Add<K, V>(K key, V val)
        {
            var pair = new Pair(typeof(K), typeof(V));
            if (!_objectMaps.ContainsKey(pair))
                throw new InvalidOperationException($"No map has pairing {typeof(K)}: {typeof(V)}");
            _objectMaps[pair][key] = val;
        }

        /// <summary>
        /// Try to get a value from a map with given key-value type pairing.
        /// </summary>
        public bool TryGet<K, V>(K key, out V val)
        {
            var pair = new Pair(typeof(K), typeof(V));
            if (!_objectMaps.ContainsKey(pair) || !_objectMaps[pair].Contains(key))
            {
                val = default;
                return false;
            }
            val = (V)_objectMaps[pair][key];
            return true;
        }

        /// <summary>
        /// Try to get a value from a map with the given key object.
        /// </summary>
        public bool TryGetObject(object key, out object val)
        {
            var k = key.GetType();
            var map = _objectMaps.FirstOrDefault(m => m.Key.k == k).Value;
            if (map == null || map.Contains(key))
            {
                val = default;
                return false;
            }
            val = map[key];
            return true;
        }

        /// <summary>
        /// Gets the value associated with the given key from the map
        /// with the matching key-value type pairing. Returns a default value if 
        /// no key is found.
        /// </summary>
        public V Get<K, V>(K key)
        {
            var pair = new Pair(typeof(K), typeof(V));
            if (!_objectMaps.ContainsKey(pair) || !_objectMaps[pair].Contains(key))
                return default;
            return (V)_objectMaps[pair][key];
        }

        /// <summary>
        /// Returns true if the given key exists.
        /// </summary>
        public bool HasKey<K>(K key)
        {
            var t = typeof(K);
            var map = _objectMaps.FirstOrDefault(m => m.Key.k == t).Value;
            if (map == null)
                return false;
            return map.Contains(key);
        }

        /// <summary>
        /// Returns the number of key-value pairs in the map with 
        /// the given key-value type pairing.
        /// </summary>
        public int Count<K, V>()
        {
            var pair = new Pair(typeof(K), typeof(V));
            if (!_objectMaps.ContainsKey(pair))
                return -1;
            return _objectMaps[pair].Count;
        }

        /// <summary>
        /// Returns an iterable of values from the map that has the given key-value type pairing.
        /// </summary>
        public IEnumerable<V> GetValues<K, V>()
        {
            var pair = new Pair(typeof(K), typeof(V));
            if (!_objectMaps.ContainsKey(pair))
                throw new ArgumentException($"No map has pairing {typeof(K)}: {typeof(V)}");
            return _objectMaps[pair].Cast<KeyValuePair<K, V>>().Select(p => p.Value);
        }

        /// <summary>
        /// Returns an iterable of keys from the map that has the given key-value type pairing.
        /// </summary>
        public IEnumerable<K> GetKeys<K, V>()
        {
            var pair = new Pair(typeof(K), typeof(V));
            if (!_objectMaps.ContainsKey(pair))
                throw new ArgumentException($"No map has pairing {typeof(K)}: {typeof(V)}");
            return _objectMaps[pair].Cast<KeyValuePair<K, V>>().Select(p => p.Key);
        }

        /// <summary>
        /// Returns an iterable of key-value pairs from the map that has the given key-value type pairing.
        /// </summary>
        public IEnumerable<KeyValuePair<K, V>> GetPairs<K, V>()
        {
            var pair = new Pair(typeof(K), typeof(V));
            if (!_objectMaps.ContainsKey(pair))
                throw new ArgumentException($"No map has pairing {typeof(K)}: {typeof(V)}");
            return _objectMaps[pair].Cast<KeyValuePair<K, V>>();
        }

        /// <summary>
        /// Removes the given key from the map with a matching key type.
        /// </summary>
        public void Remove<K>(K key)
        {
            var t = typeof(K);
            var map = _objectMaps.FirstOrDefault(m => m.Key.k == t).Value;
            if (map == null)
                return;
            map.Remove(key);
        }

        /// <summary>
        /// Clears the map with the matching key-value type pairing.
        /// </summary>
        public void Clear<K, V>()
        {
            var pair = new Pair(typeof(K), typeof(V));
            if (!_objectMaps.ContainsKey(pair))
                return;
            _objectMaps[pair].Clear();
        }

        /// <summary>
        /// Removes the map with matching key-value type pairing.
        /// </summary>
        public void RemoveMap<K, V>()
        {
            var pair = new Pair(typeof(K), typeof(V));
            if (!_objectMaps.ContainsKey(pair))
                return;
            _objectMaps.Remove(pair);
        }
    }
}