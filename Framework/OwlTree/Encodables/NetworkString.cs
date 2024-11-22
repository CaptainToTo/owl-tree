

using System;
using System.Text;

namespace OwlTree
{
    /// <summary>
    /// UTF8 string wrapper that implements IEncodable.
    /// Capacity type defines the max number of characters that can be in the string.
    /// Excess characters will be sliced off end of the string.
    /// This does not implement a majority of the string interface,
    /// and is only meant to be used to safely pass strings as RPC arguments.
    /// </summary>
    public struct NetworkString<C> : IEncodable, IVariableLength where C : ICapacity
    {
        private string _str;
        
        /// <summary>
        /// The number of characters in this string.
        /// </summary>
        public int Length { get { return _str.Length; } }

        /// <summary>
        /// The maximum number of characters allowed per this string's capacity type.
        /// </summary>
        public int Capacity { get; private set; }

        public NetworkString(string str = "")
        {
            int capacity = ((ICapacity)Activator.CreateInstance(typeof(C))).Capacity();
            if (capacity <= 0)
                throw new ArgumentException("NetworkString length must be greater than 0.");
            
            Capacity = capacity;

            if (str.Length > Capacity)
                _str = str.Substring(0, Math.Min(str.Length, Capacity));
            else
                _str = str;
        }

        public char this[int i]
        {
            get => _str[i];
        }

        public int ByteLength()
        {
            return _str == null ? 0 : Encoding.UTF8.GetByteCount(_str);
        }

        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            if (Capacity == 0)
            {
                int capacity = ((ICapacity)Activator.CreateInstance(typeof(C))).Capacity();
                if (capacity <= 0)
                    throw new ArgumentException("NetworkString length must be greater than 0.");
                
                Capacity = capacity;
            }
            _str = Encoding.UTF8.GetString(bytes.Slice(0, Math.Min(bytes.Length, MaxLength())));
            _str = _str.Substring(0, Math.Min(_str.Length, Capacity));
        }

        public void InsertBytes(Span<byte> bytes)
        {
            Encoding.UTF8.GetBytes(_str, bytes);
        }

        public int MaxLength()
        {
            if (Capacity == 0)
            {
                int capacity = ((ICapacity)Activator.CreateInstance(typeof(C))).Capacity();
                if (capacity <= 0)
                    throw new ArgumentException("NetworkString length must be greater than 0.");
                
                Capacity = capacity;
            }
            return Capacity * 4;
        }

        // operators

        public static implicit operator string(NetworkString<C> a)
        {
            return a._str;
        }

        public static implicit operator NetworkString<C>(string a)
        {
            return new NetworkString<C>(a);
        }

        public override string ToString()
        {
            return _str;
        }
    }
}