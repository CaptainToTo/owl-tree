

namespace OwlTree
{
    /// <summary>
    /// Implement to define a new network collection capacity amount.
    /// </summary>
    public interface ICapacity { public int Capacity(); }

    /// <summary>
    /// Set a collection's capacity to 8.
    /// </summary>
    public struct Capacity8 : ICapacity { public int Capacity() => 8; }
    /// <summary>
    /// Set a collection's capacity to 16.
    /// </summary>
    public struct Capacity16 : ICapacity { public int Capacity() => 16; }
    /// <summary>
    /// Set a collection's capacity to 32.
    /// </summary>
    public struct Capacity32 : ICapacity { public int Capacity() => 32; }
    /// <summary>
    /// Set a collection's capacity to 64.
    /// </summary>
    public struct Capacity64 : ICapacity { public int Capacity() => 64; }
    /// <summary>
    /// Set a collection's capacity to 128.
    /// </summary>
    public struct Capacity128 : ICapacity { public int Capacity() => 128; }
    /// <summary>
    /// Set a collection's capacity to 256.
    /// </summary>
    public struct Capacity256 : ICapacity { public int Capacity() => 256; }
    /// <summary>
    /// Set a collection's capacity to 512.
    /// </summary>
    public struct Capacity512 : ICapacity { public int Capacity() => 512; }
}