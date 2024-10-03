namespace OwlTree.Tests;

public class HuffmanTests
{
    [Fact]
    public void HistogramTest()
    {
        byte[] start = [0, 0, 1, 1, 1, 5, 120, 120, 3, 2, 1, 0, 1];
        int[] counts = Huffman.BuildHistogram(start);
        Assert.True(counts[1] == 5, "There are 5 1s");
        Assert.True(counts[0] == 3, "There are 3 0s");
    }

    [Fact]
    public void TreeTest()
    {

    }
}