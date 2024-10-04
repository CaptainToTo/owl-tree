using System.Text;

namespace OwlTree.Tests;

public class HuffmanTests
{
    [Fact]
    public void HistogramTest()
    {
        byte[] start = [0, 0, 1, 1, 1, 5, 120, 120, 3, 2, 1, 0, 1];
        int[] counts = Huffman.BuildHistogram(start, out var unique);
        Assert.True(counts[1] == 5, "There are 5 1s");
        Assert.True(counts[0] == 3, "There are 3 0s");
    }

    [Fact]
    public void NodeTest()
    {
        var root = new Huffman.Node(30, 0);
        root.right = new Huffman.Node(20, 0);
        root.left = new Huffman.Node(10, 0);
        root.right.left = new Huffman.Node(5, 0);
        root.right.right = new Huffman.Node(2, 0);
        root.left.left = new Huffman.Node(50, 0);

        var root2 = new Huffman.Node(30, 0);
        root2.right = new Huffman.Node(20, 0);
        root2.left = new Huffman.Node(10, 0);
        root2.right.left = new Huffman.Node(5, 0);
        root2.right.right = new Huffman.Node(2, 0);
        root2.left.left = new Huffman.Node(50, 0);

        Console.WriteLine(root.ToString());
        Console.WriteLine(root2.ToString());

        Assert.True(root.IsEqual(root2), "trees are the same");
    }

    [Fact]
    public void TreeTest()
    {
        int[] histogram = [1, 0, 0, 5, 3, 8, 9];
        Huffman.Node result = Huffman.BuildEncodingTree(histogram);

        Assert.True(result.ToString() == "$ 6 $ 5 $ $ 0 4 3");
    }

    [Fact]
    public void TableTest()
    {
        int[] histogram = [1, 0, 0, 5, 3, 8, 9];
        Huffman.Node result = Huffman.BuildEncodingTree(histogram);

        Assert.True(result.ToString() == "$ 6 $ 5 $ $ 0 4 3");

        Huffman.ByteEncoding[] table = new Huffman.ByteEncoding[byte.MaxValue];
        Huffman.BuildEncodingTable(table, result);

        int test = 4;
        Assert.True(table[test].encoding == 0b1101, "encoding is: " + Convert.ToString(table[test].encoding, 2));
    }

    [Fact]
    public void EncodeBytesTest()
    {
        int[] histogram = [1, 0, 0, 5, 3, 8, 9];
        Huffman.Node result = Huffman.BuildEncodingTree(histogram);

        Assert.True(result.ToString() == "$ 6 $ 5 $ $ 0 4 3");

        Huffman.ByteEncoding[] table = new Huffman.ByteEncoding[byte.MaxValue];
        Huffman.BuildEncodingTable(table, result);

        int test = 4;
        Assert.True(table[test].encoding == 0b1101, "encoding is: " + Convert.ToString(table[test].encoding, 2));

        byte[] bytes = [4, 4, 4, 6, 3, 0, 5, 6, 6, 6, 0, 3, 6, 5, 5, 6, 0];
        var compression = Huffman.Compress(bytes, table, out var bitLen);
        Assert.True(bitLen == 42, "bitLen: " + bitLen.ToString());
        int test2 = 1;
        Assert.True(compression[test2] == 0b11101101, "encoding is: " + Convert.ToString(compression[test2], 2));
    }

    [Fact]
    public void EncodeTest()
    {
        string test = @"aaaaabbbbbbbbbbbcccccceeeeeeeeeeeddddddddddddddddddssss";
        byte[] bytes = Encoding.ASCII.GetBytes(test);
        var compressed = Huffman.Encode(bytes);
        Assert.Fail(BitConverter.ToString(compressed.ToArray()));
    }

    [Fact]
    public void RebuildTreeTest()
    {
        byte[] test = [0x00, 0x00, 0x01, 0x62, 0x01, 0x65, 0x00, 0x00, 0x01, 0x63, 0x00, 0x01, 0x73, 0x01, 0x61, 0x01, 0x64];
        var tree = Huffman.RebuildTree(test, 6, out var start);
        Assert.Fail(tree.ToString());
    }

    [Fact]
    public void HuffmanTest()
    {
        var test = "aaaaaaaaaa bbbbbbbbbb cccccccccc dddddddddd eeeeeeeeee";
        var input = Encoding.ASCII.GetBytes(test);
        var compressed = Huffman.Encode(input);
        byte[] output = new byte[100];
        for (int i = 0; i < compressed.Length; i++)
            output[i] = compressed[i];

        var decompressed = Huffman.Decode(output);
        Assert.Fail(Encoding.ASCII.GetString(decompressed));
    }
}