namespace OwlTree.Tests;

public class NetworkBitSetTests
{
    [Fact]
    public void SetGetTest()
    {
        var set = new NetworkBitSet<Capacity16>();
        set.SetBit(0, true);
        set.SetBit(2, true);
        set[3] = true;
        set[6] = true;
        set[2] = false;
        Assert.True(set[2] == false);
        Assert.True(set.GetBit(6) == true);
        Assert.True(set[1] == false);
        Assert.True(set.Length == 16);
        Assert.True(set.ByteLength() == 2, set.ByteLength().ToString());
        set.Clear();
        Assert.True(set[0] == false);
    }

    [Fact]
    public void EncodeTest()
    {
        var set = new NetworkBitSet<Capacity32>();
        for(int i = 0; i < set.Length; i++)
        {
            set[i] = true;
        }

        var bytes = new byte[set.ByteLength()];
        set.InsertBytes(bytes);
        // Assert.Fail(BitConverter.ToString(bytes));
        var set2 = new NetworkBitSet<Capacity32>();
        set2.FromBytes(bytes);
        foreach (var bit in set2)
        {
            if (bit == false)
            {
                Assert.Fail("not all bits are true");
            }
        }
    }
}