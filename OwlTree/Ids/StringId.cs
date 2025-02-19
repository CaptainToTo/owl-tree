
using System;
using System.Text;

namespace OwlTree
{
    /// <summary>
    /// A 64 ASCII character string identifier. This is used for simple verification to make sure 
    /// clients attempting to connect to the server are OwlTree clients of your app. Clients that do not provide 
    /// an app id that matches the server's will be immediately rejected.
    /// </summary>
    public struct StringId : IEncodable
    {
        /// <summary>
        /// The max number of characters that can be in an id.
        /// </summary>
        public const int MaxIdLength = 64;
        /// <summary>
        /// The max number of bytes a StringId can use.
        /// </summary>
        public const int MaxByteLength = MaxIdLength + 1;
        private string _id;

        /// <summary>
        /// Creates a StringId from a max 64 ASCII character string.
        /// </summary>
        public StringId(string id)
        {
            if (Encoding.ASCII.GetByteCount(id) > MaxIdLength)
            {
                throw new ArgumentException($"Id must be a max {MaxIdLength} ASCII character string.");
            }
            _id = id.Substring(0, Math.Min(id.Length, MaxIdLength));
        }

        /// <summary>
        /// Decodes a StringId from a span of bytes.
        /// </summary>
        public StringId(ReadOnlySpan<byte> bytes)
        {
            _id = "";
            FromBytes(bytes);
        }

        /// <summary>
        /// The id string.
        /// </summary>
        public string Id => _id;

        /// <summary>
        /// The number of characters in the id.
        /// </summary>
        public int Length => _id.Length;

        public int ByteLength() => Encoding.ASCII.GetByteCount(_id) + 1;

        public int MaxLength() => MaxByteLength;

        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            _id = Encoding.ASCII.GetString(bytes.Slice(1, bytes[0]));
        }

        public void InsertBytes(Span<byte> bytes)
        {
            bytes[0] = (byte)Encoding.ASCII.GetByteCount(_id);
            Encoding.ASCII.GetBytes(_id, bytes.Slice(1));
        }

        public static bool operator ==(StringId a, StringId b)
        {
            return a._id == b._id;
        }

        public static bool operator !=(StringId a, StringId b)
        {
            return a._id != b._id;
        }

        public static implicit operator string(StringId id) => id._id;

        public static implicit operator StringId(string str) => new StringId(str);

        public override bool Equals(object obj)
        {
            return _id.Equals(obj);
        }

        public override int GetHashCode()
        {
            return _id.GetHashCode();
        }

        public override string ToString()
        {
            return "<StringId: '" + Id +"'>";
        }
    }
}