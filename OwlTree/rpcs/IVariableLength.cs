using System.Collections.Generic;
using System.Linq;
using System;

namespace OwlTree
{

/// <summary>
/// Implement with IEncodable to allow an encoding to have variable length.
/// </summary>
public interface IVariableLength
{
    internal const int LENGTH_ENCODING = 4;

    internal static void InsertLength(Span<byte> bytes, int length)
    {
        BitConverter.TryWriteBytes(bytes, (uint)length);
    }

    internal static int GetLength(ReadOnlySpan<byte> bytes)
    {
        return BitConverter.ToInt32(bytes);
    }

    /// <summary>
    /// Returns the maximum number of bytes this type of encodable can require.
    /// </summary>
    public int MaxLength();
}

}