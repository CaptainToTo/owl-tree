using System.Collections.Generic;
using System.Linq;
using System;

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
        /// Returns the number of bytes it will take to encode this object. The value returned
        /// will be used to allocate the span provided to <c>InsertBytes()</c>.
        /// </summary>
        public int ByteLength();
    }
}