
namespace OwlTree
{
    /// <summary>
    /// Implement to a make a struct or class a valid RPC argument.
    /// </summary>
    /// <typeparam name="T">The type this is implementing this interface.</typeparam>
    public interface IEncodable<T>
    {
        /// <summary>
        /// Creates a byte array representation of the object.
        /// </summary>
        public byte[] ToBytes();

        /// <summary>
        /// Inserts a byte array representation of the object into bytes, starting at index ind.
        /// </summary>
        public bool InsertBytes(ref byte[] bytes, int ind);

        /// <summary>
        /// Constructs an instance of the object from a byte array representation.
        /// </summary>
        public static abstract T FromBytes(byte[] bytes);

        /// <summary>
        /// Constructs an instance of the object from a byte array representation, starting from ind.
        /// </summary>
        public static abstract T FromBytes(byte[] bytes, int ind);
    }
}