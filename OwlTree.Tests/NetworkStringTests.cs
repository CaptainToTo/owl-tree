namespace OwlTree.Tests;

public class NetworkStringTests
{
    [Fact]
    public void ConversionTest()
    {
        NetworkString<Capacity8> netStr = "Hello World";
        Assert.True(netStr == "Hello Wo", netStr);
        string str = netStr + "rld";
        Assert.True(str == "Hello World", str);
    }

    [Fact]
    public void EncodeTest()
    {
        NetworkString<Capacity32> netStr = "Hello World, foo bar baz, fizz buzz";
        var bytes = new byte[netStr.ByteLength()];
        netStr.InsertBytes(bytes);
        NetworkString<Capacity32> netStr2 = "";
        netStr2.FromBytes(bytes);
        Assert.True(netStr2 == "Hello World, foo bar baz, fizz b", netStr2);
    }
}