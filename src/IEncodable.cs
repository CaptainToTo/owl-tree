
namespace OwlTree
{
    /// <summary>
    /// Implement to a make a struct or class a valid RPC argument.
    /// </summary>
    public interface IEncodable
    {
        /// <summary>
        /// Creates a byte array representation of the object.
        /// </summary>
        public byte[] ToBytes();

        /// <summary>
        /// Inserts a byte array representation of the object into bytes, starting at index ind.
        /// ind should be updated to be after the last byte inserted.<br />
        /// Return true if the bytes were successfully inserted, false otherwise.
        /// </summary>
        public bool InsertBytes(ref byte[] bytes, ref int ind);

        /// <summary>
        /// Returns the expected number of bytes it will take to encode this object.
        /// </summary>
        public int ExpectedLength();

        /// <summary>
        /// Constructs an instance of the object from a byte array representation.
        /// </summary>
        public static abstract object FromBytes(byte[] bytes);

        /// <summary>
        /// Constructs an instance of the object from a byte array representation, starting from ind.
        /// ind should be updated to be after the last byte read.
        /// </summary>
        public static abstract object FromBytes(byte[] bytes, ref int ind);
    }
}