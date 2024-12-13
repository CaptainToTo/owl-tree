namespace OwlTree.Tests;

public class NetworkDictTests
{
    [Fact]
    public void BasicFuncTest()
    {
        NetworkDict<Capacity16, string, int> dict = new NetworkDict<Capacity16, string, int>();
        dict.Add("a", 1);
        dict.Add("b", 2);
        dict.Add("c", 3);
        dict.Add("d", 4);
        dict.Remove("c");
        Assert.True(dict.ContainsKey("c") == false);
        Assert.True(dict.Count == 3);
        Assert.True(dict["a"] == 1);

        dict.Clear();

        Assert.True(dict.Count == 0);
        Assert.True(dict.Capacity == 16);
    }

    [Fact]
    public void CapacityTest()
    {
        var list = new NetworkDict<Capacity16, string, int>();
        var errored = false;
        for (int i = 0; i < 40; i++)
        {
            try{
                list.Add(i.ToString(), i);
            }
            catch {
                errored = true;
            }
            if (errored)
                break;
        }
        Assert.True(list.Count == 16);
    }

    [Fact]
    public void EncodeTest()
    {
        NetworkDict<Capacity16, string, int> dict = new NetworkDict<Capacity16, string, int>();
        dict.Add("a", 1);
        dict.Add("b", 2);
        dict.Add("c", 3);
        dict.Add("d", 4);

        var bytes = new byte[dict.ByteLength()];
        dict.InsertBytes(bytes);

        var dict2 = new NetworkDict<Capacity16, string, int>();
        dict2.FromBytes(bytes);

        string str = "";
        foreach (var pair in dict2)
        {
            str += pair.Key.ToString() + ": " + pair.Value.ToString() + ", ";
        }
        Assert.Fail(str);
    }

    [Fact]
    public void ListTest()
    {
        var dict = new NetworkDict<Capacity8, ClientId, NetworkList<Capacity8, NetworkId>>();

        var list1 = new NetworkList<Capacity8, NetworkId>();
        list1.Add(NetworkId.New());
        list1.Add(NetworkId.New());
        list1.Add(NetworkId.New());

        var list2 = new NetworkList<Capacity8, NetworkId>();
        list2.Add(NetworkId.New());
        list2.Add(NetworkId.New());
        list2.Add(NetworkId.New());

        dict.Add(new ClientId(1), list1);
        dict.Add(new ClientId(2), list2);

        var bytes = new byte[dict.ByteLength()];
        dict.InsertBytes(bytes);

        var dict2 = new NetworkDict<Capacity8, ClientId, NetworkList<Capacity8, NetworkId>>();
        dict2.FromBytes(bytes);

        string str = "";
        foreach (var pair in dict2)
        {
            str += pair.Key.ToString() + ": [";
            foreach (var id in pair.Value)
            {
                str += id.ToString() + ", ";
            }
            str += "] ; ";
        }
        Assert.Fail(str);
    }
}