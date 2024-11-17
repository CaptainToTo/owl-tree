

using System;
using System.Collections;
using System.Collections.Generic;

namespace OwlTree
{
    /// <summary>
    /// A fixed size bit set that implements IEncodable.
    /// Bit count is defined by capacity type.
    /// </summary>
    public class NetworkBitSet<C> : IEncodable, IEnumerable<bool> where C : ICapacity
    {
        private byte[] _set;

        /// <summary>
        /// The number of bits available in the set.
        /// </summary>
        public int Length { get; private set; }

        public NetworkBitSet()
        {
            int capacity = ((ICapacity)Activator.CreateInstance(typeof(C))).Capacity();
            if (capacity <= 0)
                throw new ArgumentException("NetworkBitSet length must be greater than 0.");
            
            Length = capacity;

            _set = new byte[(Length / 8) + ((Length % 8) != 0 ? 1 : 0)];
        }

        /// <summary>
        /// Set the bit at the given index.
        /// </summary>
        public void SetBit(int ind, bool state)
        {
            if (ind < 0 || Length <= ind)
                throw new ArgumentException("Index must be between 0 and the bit set's length.");
            
            if (state)
            {
                _set[ind / 8] |= (byte)(0x1 << (ind % 8));
            }
            else
            {
                _set[ind / 8] &= (byte)~(0x1 << (ind % 8));
            }
        }

        /// <summary>
        /// Returns true if the bit at the given index is set.
        /// </summary>
        public bool GetBit(int ind)
        {
            if (ind < 0 || Length <= ind)
                throw new ArgumentException("Index must be between 0 and the bit set's length.");
            
            return (_set[ind / 8] & (byte)(0x1 << (ind % 8))) != 0;
        }

        public bool this[int i]
        {
            get => GetBit(i);
            set => SetBit(i, value);
        }

        /// <summary>
        /// Fill the bit set from the starting index, for length bits,
        /// with the given value.
        /// </summary>
        public void Fill(bool value, int start = 0, int length = -1)
        {
            int count = length < 0 ? Length - start : length;

            for (int i = 0; i < count; i++)
            {
                SetBit(start + i, value);
            }
        }

        /// <summary>
        /// Reset the bit set.
        /// </summary>
        public void Clear()
        {
            Fill(false);
        }

        public IEnumerator<bool> GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public class Enumerator : IEnumerator<bool>
        {
            public bool Current => _set.GetBit(_curInd);
            object IEnumerator.Current => _set.GetBit(_curInd);

            int _curInd = 0;
            NetworkBitSet<C> _set;

            public Enumerator(NetworkBitSet<C> set)
            {
                _set = set;
            }

            public void Dispose()
            {
                
            }

            public bool MoveNext()
            {
                _curInd++;
                return _curInd < _set.Length;
            }

            public void Reset()
            {
                _curInd = 0;
            }
        }

        public int ByteLength()
        {
            return _set.Length;
        }

        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            for (int i = 0; i < Math.Min(bytes.Length, _set.Length); i++)
            {
                _set[i] = bytes[i];
            }
        }

        public void InsertBytes(Span<byte> bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = _set[i];
            }
        }

        public override string ToString()
        {
            return "<NetworkBitSet; Length: " + Length + "; Byte Count: " + _set.Length + ">";
        }
    }
}