namespace OwlTree.Tests;

public class NetworkListTests
{
    [Fact]
    public void BasicFuncTest()
    {
        var list = new NetworkList<Capacity32, int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);
        list.Add(4);
        list.Add(5);
        list.Add(6);
        list.Remove(5);
        Assert.True(list.IndexOf(6) == 4, list.IndexOf(6).ToString());

        Assert.True(list.Count == 5);
        Assert.True(list.Capacity == 32);

        list.Clear();
        Assert.True(list.Count == 0);
    }

    [Fact]
    public void CapacityTest()
    {
        var list = new NetworkList<Capacity32, int>();
        var errored = false;
        for (int i = 0; i < 40; i++)
        {
            try{
                list.Add(i);
            }
            catch {
                errored = true;
            }
            if (errored)
                break;
        }
        Assert.True(list.Count == 32);
    }

    [Fact]
    public void EncodeTest()
    {
        var list = new NetworkList<Capacity8, NetworkVec2>();
        list.Add(new NetworkVec2(1.44f, 2));
        list.Add(new NetworkVec2(3, 4));
        list.Add(new NetworkVec2(5, 6));
        list.Add(new NetworkVec2(7, 8));

        var bytes = new byte[list.ByteLength()];
        list.InsertBytes(bytes);

        var list2 = new NetworkList<Capacity8, NetworkVec2>();
        list2.FromBytes(bytes);

        string str = "";
        foreach (var v in list2)
        {
            str += v.ToString() + " ";
        }

        Assert.Fail(str);
    }

    [Fact]
    public void MatrixTest()
    {
        var list = new NetworkList<Capacity8, NetworkList<Capacity8, NetworkVec4>>();
        var row1 = new NetworkList<Capacity8, NetworkVec4>();
        row1.Add(new NetworkVec4(1, 2, 3, 4));
        row1.Add(new NetworkVec4(1, 2, 3, 4));
        row1.Add(new NetworkVec4(1, 2, 3, 4));
        var row2 = new NetworkList<Capacity8, NetworkVec4>();
        row2.Add(new NetworkVec4(1, 2, 3, 4));
        row2.Add(new NetworkVec4(1, 2, 3, 4));
        row2.Add(new NetworkVec4(1, 2, 3, 4));

        list.Add(row1);
        list.Add(row2);

        var bytes = new byte[list.ByteLength()];
        list.InsertBytes(bytes);

        // Assert.Fail(BitConverter.ToString(bytes));

        var list2 = new NetworkList<Capacity8, NetworkList<Capacity8, NetworkVec4>>();
        list2.FromBytes(bytes);

        string str = "";
        foreach (var r in list2)
        {
            foreach(var v in r)
            {
                str += v.ToString();
            }
            str += "\n";
        }

        Assert.Fail("\n" + str);
    }
}