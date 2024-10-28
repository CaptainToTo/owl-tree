
using System;
using System.Collections;
using System.Collections.Generic;

namespace OwlTree
{
    /// <summary>
    /// A fixed size array wrapper that implements IEncodable.
    /// Array size is defined by capacity type.
    /// </summary>
    public class NetworkArray<C, T> : IEncodable, IVariableLength, IEnumerable<T> where C : ICapacity
    {
        private T[] _arr;

        /// <summary>
        /// The number of elements this array can hold, defined by its capacity type.
        /// </summary>
        public int Length { get { return _arr.Length; } }

        private int _maxLen;
        private bool _isVariable;

        public NetworkArray()
        {
            int capacity = ((ICapacity)Activator.CreateInstance(typeof(C))).Capacity();
            if (capacity <= 0)
                throw new ArgumentException("NetworkArray length must be greater than 0.");
            
            if (!RpcProtocol.IsEncodableParam(typeof(T)))
            {
                throw new ArgumentException("NetworkArray must have an encodable type.");
            }

            _arr = new T[capacity];

            _maxLen = capacity * (4 + RpcProtocol.GetMaxLength(typeof(T)));

            _isVariable = typeof(T) == typeof(IVariableLength);
        }

        /// <summary>
        /// Reset this array's elements from the starting index, for length elements.
        /// If no arguments are provided, the full array will be cleared.
        /// </summary>
        public void Clear(int start = 0, int length = -1)
        {
            Array.Clear(_arr, start, length == -1 ? _arr.Length - start : length);
        }

        /// <summary>
        /// Fill this array with the given value from the starting index, for length elements.
        /// If no arguments are provided, all indices will be filled.
        /// </summary>
        public void Fill(T val, int start = 0, int length = -1)
        {
            Array.Fill(_arr, val, start, length == -1 ? _arr.Length - start : length);
        }

        public T this[int i]
        {
            get => _arr[i];
            set => _arr[i] = value;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return (IEnumerator<T>)_arr.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int ByteLength()
        {
            int total = 0;
            foreach (var elem in this)
            {
                total += 4 + RpcProtocol.GetExpectedLength(elem);
            }
            return total;
        }

        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            Clear();

            for (int i = 0; i < bytes.Length;)
            {
                int ind = BitConverter.ToInt32(bytes.Slice(i));
                i += 4;

                int len = 0;
                if (_isVariable)
                {
                    len = BitConverter.ToInt32(bytes.Slice(i));
                    i += 4;
                }
                else
                {
                    len = RpcProtocol.GetMaxLength(typeof(T));
                }
                _arr[ind] = (T)RpcProtocol.DecodeObject(bytes.Slice(i, len), ref i, typeof(T));
            }
        }

        public bool InsertBytes(Span<byte> bytes)
        {

            int ind = 0;
            for (int i = 0; i < Length; i++)
            {
                var elem = _arr[i];
                BitConverter.TryWriteBytes(bytes.Slice(ind), i);
                ind += 4;
                int len = RpcProtocol.GetExpectedLength(elem);
                if (_isVariable)
                {
                    BitConverter.TryWriteBytes(bytes.Slice(ind), len);
                    ind += 4;
                }
                RpcProtocol.InsertBytes(bytes.Slice(ind, len), elem);
                ind += len;
            }

            return true;
        }

        public int MaxLength()
        {
            return _maxLen;
        }

        public override string ToString()
        {
            return "<NetworkArray<" + typeof(T).ToString() + ">; Length: " + Length + ">";
        }
    }
}