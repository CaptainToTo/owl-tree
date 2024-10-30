namespace OwlTree.Tests;

public class VectorTests
{
    [Fact]
    public void Vector2Test()
    {
        var a = new NetworkVec2(1.25f, 6.3f);
        var b = new NetworkVec2(0.75f, 2.034f);
        var c = new NetworkVec2(0, 1);
        Assert.True(c.Magnitude() == 1, c.Magnitude().ToString());

        var bytes = new byte[a.ByteLength()];
        a.InsertBytes(bytes);
        b.FromBytes(bytes);
        Assert.Fail(b.ToString());
    }

    [Fact]
    public void Vector3Test()
    {
        var a = new NetworkVec3(1.25f, 6.3f, 0.0034f);
        var b = new NetworkVec3(0.75f, 2.034f, 1.023f);
        var c = new NetworkVec3(0, 1, 0);
        Assert.True(c.Magnitude() == 1, c.Magnitude().ToString());

        var bytes = new byte[a.ByteLength()];
        a.InsertBytes(bytes);
        b.FromBytes(bytes);
        Assert.Fail(b.ToString());
    }

    [Fact]
    public void Vector4Test()
    {
        var a = new NetworkVec4(1.25f, 6.3f, 0.0034f, 12);
        var b = new NetworkVec4(0.75f, 2.034f, 1.023f, 31);
        var c = new NetworkVec4(0, 1, 0, 0);
        Assert.True(c.Magnitude() == 1, c.Magnitude().ToString());

        var bytes = new byte[a.ByteLength()];
        a.InsertBytes(bytes);
        b.FromBytes(bytes);
        Assert.Fail(b.ToString());
    }
}