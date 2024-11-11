
namespace OwlTree.Tests;

public class RpcEncodingTests
{
    public struct TestEncodable : IEncodable
    {
        int i;
        public int ByteLength()
        {
            return 4;
        }

        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            i = BitConverter.ToInt32(bytes);
        }

        public void InsertBytes(Span<byte> bytes)
        {
            BitConverter.TryWriteBytes(bytes, i);
        }
    }

    public class TestEncodableClass : IEncodable
    {
        int i;
        public int ByteLength()
        {
            return 4;
        }

        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            i = BitConverter.ToInt32(bytes);
        }

        public void InsertBytes(Span<byte> bytes)
        {
            BitConverter.TryWriteBytes(bytes, i);
        }
    }

    [Fact]
    public void LengthTest()
    {

    }
}