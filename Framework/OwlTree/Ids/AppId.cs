
using System;
using System.Text;

namespace OwlTree
{
    /// <summary>
    /// A 64 ASCII character string identifier for your app. This is used for simple verification to make sure 
    /// clients attempting to connect to the server are OwlTree clients of your app. Clients that do not provide 
    /// an app id that matches the server's will be immediately rejected.
    /// </summary>
    public struct AppId : IEncodable
    {
        private string _id;

        /// <summary>
        /// Creates an AppId from a max 64 ASCII character string.
        /// </summary>
        public AppId(string id)
        {
            if (Encoding.ASCII.GetByteCount(id) > 64)
            {
                throw new ArgumentException("Id must be a max 64 ASCII character string.");
            }
            _id = id.Substring(0, Math.Min(id.Length, 64));
            if (_id.Length < 64)
            {
                _id += new string('_', 64 - _id.Length);
            }
        }

        /// <summary>
        /// The id string. If the given string at construction was shorter than 64 characters,
        /// the remaining characters have been filled by underscores "_".
        /// </summary>
        public string Id { get { return _id; } }

        public int ByteLength()
        {
            return 64;
        }

        public static int MaxLength()
        {
            return 64;
        }

        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            _id = Encoding.ASCII.GetString(bytes.Slice(0, MaxLength()));
        }

        public void InsertBytes(Span<byte> bytes)
        {
            Encoding.ASCII.GetBytes(_id, bytes);;
        }

        public static bool operator ==(AppId a, AppId b)
        {
            return a._id == b._id;
        }

        public static bool operator !=(AppId a, AppId b)
        {
            return a._id != b._id;
        }

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
            return Id;
        }
    }
}