
using System.Collections.Generic;

namespace OwlTree
{

public static class Huffman
{
    internal struct ByteEncoding
    {
        public byte value;
        public byte encoding;
        public int bitLen = -1;

        public ByteEncoding() {}

        public bool HasValue { get { return bitLen != -1; } }

        public void Insert(Span<byte> bytes, int startBitIndex)
        {
            int byteIndex = startBitIndex / 8;
            int bitIndex = startBitIndex % 8;

            for (int i = 0; i < bitLen; i++)
            {
                byte bit = (byte)((encoding & (1 << i)) != 0 ? 1 : 0);
                bytes[byteIndex] |= (byte)(bit << bitIndex);

                bitIndex++;
                if (bitIndex % 8 == 0)
                {
                    byteIndex++;
                    bitIndex = 0;
                }
            }
        }
    }

    internal class Node
    {
        public bool isLeaf;
        public byte value;
        public int prob;
        public Node? left;
        public Node? right;

        public Node(byte x, int prob, bool leaf=true)
        {
            value = x;
            this.prob = prob;
            isLeaf = leaf;
        }

        public int Size()
        {
            return 1 + (left?.Size() ?? 0) + (right?.Size() ?? 0);
        }

        public bool IsEqual(Node other)
        {
            return RecurseEquals(this, other);
        }

        private static bool RecurseEquals(Node? a, Node? b)
        {
            if (a == null && b == null)
                return true;

            if (a == null || b == null)
                return false;

            if (a.value != b.value)
                return false;

            if (!RecurseEquals(a.left, b.left))
                return false;
            else if (!RecurseEquals(a.right, b.right))
                return false;
            
            return true;
        }

        public override string ToString()
        {
            return (isLeaf ? value.ToString() : '$') + (left != null ? " " + left.ToString() : "") + (right != null ? " " + right.ToString() : "");
        }

        public void Encode(Span<byte> bytes, ref int ind)
        {
            if (isLeaf)
            {
                bytes[ind] = 1;
                bytes[ind + 1] = value;
                ind += 2;
            }
            else
            {
                bytes[ind] = 0;
                ind++;
                if (left != null)
                    left.Encode(bytes, ref ind);
                
                if (right != null)
                    right.Encode(bytes, ref ind);
            }
        }
    }

    const uint HEADER = 0xaabbccee;

    public static void Encode(Span<byte> bytes)
    {
        var histogram = BuildHistogram(bytes, out var unique);
        var tree = BuildEncodingTree(histogram);
        var table = new ByteEncoding[byte.MaxValue];
        BuildEncodingTable(table, tree);
        var compression = Compress(bytes, table, out var bitLen);

        if (bytes.Length < 12 + (tree.Size() * 2) + (bitLen / 8) + 1)
            return;

        BitConverter.TryWriteBytes(bytes, HEADER);
        BitConverter.TryWriteBytes(bytes.Slice(4), bytes.Length);
        BitConverter.TryWriteBytes(bytes.Slice(8), bitLen);
        int treeInd = 12;
        tree.Encode(bytes, ref treeInd);
        for (int i = 0; i < (bitLen / 8) + 1; i++)
        {
            var b = compression[i];
            bytes[treeInd] = b;
            treeInd++;
        }
        for (; treeInd < bytes.Length; treeInd++)
        {
            bytes[treeInd] = 0;
        }
    }

    internal static int[] BuildHistogram(Span<byte> bytes, out int unique)
    {
        int[] histogram = new int[byte.MaxValue + 1];
        unique = 0;

        for (int i = 0; i < bytes.Length; i++)
        {
            if (histogram[bytes[i]] == 0)
                unique++;
            histogram[bytes[i]]++;
        }

        return histogram;
    }

    internal static Node BuildEncodingTree(int[] histogram)
    {
        Node root = new Node(0, 0);

        PriorityQueue<Node, int> q = new PriorityQueue<Node, int>(histogram.Length / 2);
        for (int i = 0; i < histogram.Length; i++)
        {
            if (histogram[i] > 0)
                q.Enqueue(new Node((byte)i, histogram[i]), histogram[i]);
        }

        while (q.Count > 1)
        {
            var a = q.Dequeue();
            var b = q.Dequeue();
            var parent = new Node(0, a.prob + b.prob, false);
            parent.left = a;
            parent.right = b;
            q.Enqueue(parent, parent.prob);
        }

        root = q.Dequeue();

        return root;
    }

    internal static void BuildEncodingTable(ByteEncoding[] table, Node? tree, byte encoding=0, int bitLen=0)
    {
        if (tree == null)
            return;
        
        if (tree.isLeaf)
        {
            table[tree.value].value = tree.value;
            table[tree.value].encoding = encoding;
            table[tree.value].bitLen = bitLen;
        }
        else
        {
            var rightEncoding = (byte)((encoding << 1) | 0x1);
            BuildEncodingTable(table, tree.left, (byte)(encoding << 1), bitLen + 1);
            BuildEncodingTable(table, tree.right, rightEncoding, bitLen + 1);
        }
    }

    internal static byte[] Compress(Span<byte> bytes, ByteEncoding[] table, out int bitLength)
    {
        byte[] compression = new byte[bytes.Length];
        bitLength = 0;

        for (int i = 0; i < bytes.Length; i++)
        {
            table[bytes[i]].Insert(compression, bitLength);
            bitLength += table[bytes[i]].bitLen;
        }

        return compression;
    }

    public static void Decode(Span<byte> bytes)
    {

    }
}

}