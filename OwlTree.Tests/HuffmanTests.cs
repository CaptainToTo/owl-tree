using System.Text;

namespace OwlTree.Tests;

public class HuffmanTests
{
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
        Assert.True(Encoding.ASCII.GetString(decompressed) == test, Encoding.ASCII.GetString(decompressed));
    }

    [Fact]
    public void HuffmanTest2()
    {
        var test = "she sells sea shells by the sea shore";
        var input = Encoding.ASCII.GetBytes(test);
        var compressed = Huffman.Encode(input);
        byte[] output = new byte[100];
        for (int i = 0; i < compressed.Length; i++)
            output[i] = compressed[i];

        var decompressed = Huffman.Decode(output);
        Assert.True(Encoding.ASCII.GetString(decompressed) == test, Encoding.ASCII.GetString(decompressed) + "\n" + BitConverter.ToString(decompressed.ToArray()));
    }
}