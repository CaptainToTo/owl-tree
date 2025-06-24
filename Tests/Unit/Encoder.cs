using OwlTree;

namespace Unit;

public class EncoderTests
{

    public struct TestStruct : IEncodable
    {
        public bool x;
        public int y;
        public double z;
        public short w;

        public int ByteLength()
        {
            return Encoder.AutoByteLength(x, y, z, w);
        }

        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            Encoder.AutoFromBytes(bytes, ref x)
                .AutoFromBytes(bytes, ref y)
                .AutoFromBytes(bytes, ref z)
                .AutoFromBytes(bytes, ref w);
        }

        public void InsertBytes(Span<byte> bytes)
        {
            Encoder.AutoInsertBytes(bytes, x, y, z, w);
        }
    }

    [Fact]
    public void AutoEncoding()
    {
        int x = 1; // 4
        float y = 3.14f; // 4
        double z = 20.20f; // 8
        string w = "hello world"; // 12
        NetworkVec3 a = NetworkVec3.One; // 12

        var result = Encoder.AutoByteLength(x, y, z, w, a);

        Assert.True(result == 40, "auto byte length did not return 40, instead got " + result);

        var bytes = new byte[result];

        Encoder.AutoInsertBytes(bytes, x, y, z, w, a);

        int x1 = 0;
        float y1 = 0;
        double z1 = 0;
        string w1 = "";
        NetworkVec3 a1 = NetworkVec3.Zero;

        Encoder.AutoFromBytes(bytes, ref x1)
            .AutoFromBytes(bytes, ref y1)
            .AutoFromBytes(bytes, ref z1)
            .AutoFromBytes(bytes, ref w1)
            .AutoFromBytes(bytes, ref a1);

        Assert.True(x1 == x, "decoded x isn't correct. Got " + x1 + ", should be " + x);
        Assert.True(y1 == y, "decoded y isn't correct. Got " + y1 + ", should be " + y);
        Assert.True(z1 == z, "decoded z isn't correct. Got " + z1 + ", should be " + z);
        Assert.True(w1 == w, "decoded w isn't correct. Got " + w1 + ", should be " + w);
        Assert.True(a1 == a, "decoded a isn't correct. Got " + a1 + ", should be " + a);

        var test = new TestStruct();
        test.x = true;
        test.y = 5;
        test.z = 500;
        test.w = 300;

        var result2 = test.ByteLength();

        Assert.True(result2 == 15, "test struct did not return a length of 15, instead got " + result2);

        var bytes2 = new byte[result2];

        test.InsertBytes(bytes2);

        var decode = new TestStruct();
        decode.FromBytes(bytes2);

        Assert.True(decode.x == test.x, "decode of test struct doesn't have the same x value, got " + decode.x);
        Assert.True(decode.y == test.y, "decode of test struct doesn't have the same y value, got " + decode.y);
        Assert.True(decode.z == test.z, "decode of test struct doesn't have the same z value, got " + decode.z);
        Assert.True(decode.w == test.w, "decode of test struct doesn't have the same w value, got " + decode.w);
    }

}