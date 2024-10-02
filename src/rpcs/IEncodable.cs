
namespace OwlTree
{
    /// <summary>
    /// Implement to a make a struct or class a valid RPC argument.
    /// </summary>
    public interface IEncodable
    {
        internal static IEnumerable<Type> GetEncodableTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsValueType && !t.IsAbstract && typeof(IEncodable).IsAssignableFrom(t));
        }

        /// <summary>
        /// Inserts a byte array representation of the object into bytes, starting at index ind.
        /// ind should be updated to be after the last byte inserted.<br />
        /// Return true if the bytes were successfully inserted, false otherwise.
        /// </summary>
        public bool InsertBytes(Span<byte> bytes);

        /// <summary>
        /// Fill an empty version of this object from the provided bytes encoding.
        /// </summary>
        public void FromBytes(ReadOnlySpan<byte> bytes);

        /// <summary>
        /// Returns the expected number of bytes it will take to encode this object.
        /// </summary>
        public int ExpectedLength();

        /// <summary>
        /// Returns the max length this type of IEncodable can be.
        /// </summary>
        public static abstract int MaxLength();
    }
}