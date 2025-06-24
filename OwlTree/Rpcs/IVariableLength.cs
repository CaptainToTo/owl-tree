using System.Collections.Generic;
using System.Linq;
using System;

namespace OwlTree
{

/// <summary>
/// Implement with IEncodable to allow an encoding to have variable length.
/// </summary>
public interface IVariableLength : IEncodable
{
    /// <summary>
    /// The number of bytes an IVariableLength length number will take up in the encoding.
    /// </summary>
    internal const int LengthEncoding = 4;

    internal static void InsertLength(Span<byte> bytes, int length)
    {
        Encoder.InsertBytes(bytes, (uint)length);
    }

    internal static int GetLength(ReadOnlySpan<byte> bytes)
    {
        return Encoder.DecodeInt32(bytes);
    }

    /// <summary>
    /// Returns the maximum number of bytes this type of encodable can require.
    /// </summary>
    public int MaxLength();
}

}