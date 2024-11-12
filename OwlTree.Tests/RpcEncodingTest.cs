
namespace OwlTree.Tests;

public class RpcEncodingTests
{
    public struct TestEncodable : IEncodable
    {
        public int i;
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
        public int i;
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
    public void IsEncodableTest()
    {
        Assert.True(RpcEncoding.IsEncodable(typeof(int)));
        Assert.True(RpcEncoding.IsEncodable(typeof(short)));
        Assert.True(RpcEncoding.IsEncodable(typeof(TestEncodable)));
    }

    [Fact]
    public void LengthTest()
    {
        Assert.True(RpcEncoding.GetMaxLength(typeof(TestEncodableClass)) == 4);
        Assert.True(RpcEncoding.GetMaxLength(typeof(double)) == 8);

        int a = 8;
        byte b = 1;
        object[] c = {a, b};
        Assert.True(RpcEncoding.GetExpectedLength(a) == 4);
        Assert.True(RpcEncoding.GetExpectedLength(b) == 1);
        Assert.True(RpcEncoding.GetExpectedLength(c) == 5);

        Assert.True(RpcEncoding.GetExpectedRpcLength(c) == 13);
    }

    [Fact]
    public void EncodeAndDecodeObjTest()
    {
        TestEncodable a = new TestEncodable();
        var arr = new byte[20];
        var span = arr.AsSpan(0, RpcEncoding.GetExpectedLength(a));
        Assert.True(span.Length == 4, "span not 4 bytes");

        RpcEncoding.InsertBytes(span, a);
        var b = RpcEncoding.DecodeObject(span, typeof(TestEncodable), out var len);
        Assert.True(typeof(TestEncodable) == b.GetType(), "decoded isn't struct");
        Assert.True(len == 4, "length not 4");
        Assert.True(a.i == ((TestEncodable)b).i, "value encoding isn't value decoded");
    }

    [Fact]
    public void EncodeAndDecodeIntTest()
    {
        int a = 5;
        var arr = new byte[20];
        var span = arr.AsSpan(0, RpcEncoding.GetExpectedLength(a));
        Assert.True(span.Length == 4, "span not 4 bytes");

        RpcEncoding.InsertBytes(span, a);
        var b = RpcEncoding.DecodeObject(span, typeof(int), out var len);
        Assert.True(typeof(int) == b.GetType(), "decoded isn't int");
        Assert.True(len == 4, "length not 4");
        Assert.True(a == (int)b, "value encoding isn't value decoded");
    }

    [Fact]
    public void EncodeAndDecodeRpcTest()
    {
        var id = new RpcId(11);
        var target = new NetworkId(10);
        var args = new object[]{10, 3.0f, 0.12, 3U};
        var paramTypes = new Type[]{typeof(int), typeof(float), typeof(double), typeof(uint)};

        var bytes = new byte[RpcEncoding.GetExpectedRpcLength(args)];

        Assert.True(bytes.Length == 28, "bytes not correct length");

        RpcEncoding.EncodeRpc(bytes, id, target, args);
        var output = RpcEncoding.DecodeRpc(ClientId.None, bytes, paramTypes, out var outId, out var outTarget);

        Assert.True(id == outId, "rpc id mismatch");
        Assert.True(target == outTarget, "target id mismatch");
        Assert.True((int)output[0] == (int)args[0], "int not matching: " + args[0] + " -> " + output[0]);
        Assert.True((uint)output[3] == (uint)args[3], "uint not matching" + args[3] + " -> " + output[3]);
    }
}