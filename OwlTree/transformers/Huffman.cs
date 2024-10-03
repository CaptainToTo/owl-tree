

namespace OwlTree
{

public static class Huffman
{
    private class Node
    {
        public byte value;
        public Node? left;
        public Node? right;

        public Node(byte x)
        {
            value = x;
        }
    }


    public static void Encode(Span<byte> bytes)
    {
        var histogram = BuildHistogram(bytes);
        var tree = BuildEncodingTree(histogram);
    }

    private static int[] BuildHistogram(Span<byte> bytes)
    {
        int[] histogram = new int[byte.MaxValue + 1];

        for (int i = 0; i < bytes.Length; i++)
        {
            histogram[bytes[i]]++;
        }

        return histogram;
    }

    private static Node BuildEncodingTree(int[] histogram)
    {
        Node root = new Node(0);



        return root;
    }

    public static void Decode(Span<byte> bytes)
    {

    }
}

}