
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
        public Node? parent;

        public Node(byte x, int prob, bool leaf=true)
        {
            value = x;
            this.prob = prob;
            isLeaf = leaf;
        }

        public Node AddChild(byte x, int prob, bool leaf=true)
        {
            if (left == null)
            {
                left = new Node(x, prob, leaf);
                left.parent = this;
                return left;
            }
            else
            {
                right = new Node(x, prob, leaf);
                right.parent = this;
                return right;
            }
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
            return (isLeaf ? Convert.ToString(value, 16) : '$') + (left != null ? " " + left.ToString() : "") + (right != null ? " " + right.ToString() : "");
        }

        // TODO: improve space needed for tree encoding
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

    // * Encode ============================================

    /// <summary>
    /// Tries to compress the given bytes using Huffman Coding. If the number of bytes is too small
    /// to reasonably compress, then the same Span provided as an argument is returned. If the bytes were 
    /// compressed, then a new span will be returned that has a smaller length than the original.
    /// <br /> <br />
    /// Since Encoding takes a Span, compression is done in-place, and will override the contents of the 
    /// original.
    /// </summary>
    public static Span<byte> Encode(Span<byte> bytes)
    {
        var histogram = BuildHistogram(bytes, out var unique);
        var tree = BuildEncodingTree(histogram);
        var table = new ByteEncoding[byte.MaxValue];
        BuildEncodingTable(table, tree);
        var compression = Compress(bytes, table, out var bitLen);

        var size = tree.Size();
        if (bytes.Length < 13 + (size * 2) + (bitLen / 8) + 1)
            return bytes;

        BitConverter.TryWriteBytes(bytes, HEADER);
        BitConverter.TryWriteBytes(bytes.Slice(4), bytes.Length);
        BitConverter.TryWriteBytes(bytes.Slice(8), bitLen);
        bytes[12] = (byte) unique;
        int treeInd = 13;
        tree.Encode(bytes, ref treeInd);
        for (int i = 0; i < (bitLen / 8) + 1; i++)
        {
            var b = compression[i];
            bytes[treeInd] = b;
            treeInd++;
        }
        return bytes.Slice(0, treeInd);
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
            var rightEncoding = (byte)(encoding | (0x1 << bitLen));
            BuildEncodingTable(table, tree.left, encoding, bitLen + 1);
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

    // * =================================================

    // * Decode ==========================================

    /// <summary>
    /// Tries to decompress a span of bytes that were compressed using <c>Huffman.Encode()</c>.
    /// <br /> <br />
    /// Since Encoding takes a Span, compression is done in-place, and will override the contents of the 
    /// original.
    /// </summary>
    public static Span<byte> Decode(Span<byte> bytes)
    {
        var header = BitConverter.ToUInt32(bytes);
        if (header != HEADER)
        {
            return bytes;
        }

        var originalLen = BitConverter.ToInt32(bytes.Slice(4));
        var bitLen = BitConverter.ToInt32(bytes.Slice(8));
        var size = bytes[12];

        if (originalLen > bytes.Length)
        {
            return bytes;
        }
        
        Node tree = RebuildTree(bytes.Slice(13), size, out var start);
        var decompressed = Decompress(bytes.Slice(13 + start), tree, originalLen, bitLen);

        for (int i = 0; i < decompressed.Length; i++)
        {
            bytes[i] = decompressed[i];
        }

        return decompressed;
    }

    internal static Node RebuildTree(Span<byte> bytes, int size, out int last)
    {
        Node root = new Node(0, 0, false);

        Node cur = root;
        int curByte = 1;
        int curNode = 1;
        do {

            if (bytes[curByte] == 0)
            {
                cur = cur.AddChild(0, 0, false);
                curByte += 1;
            }
            else
            {
                cur.AddChild(bytes[curByte + 1], 0);
                while (cur.right != null && cur.parent != null)
                {
                    cur = cur.parent;
                }
                curByte += 2;
                curNode++;
            }

        } while (curNode <= size);
        last = curByte;

        return root;
    }

    internal static byte[] Decompress(Span<byte> bytes, Node tree, int originalLen, int bitLen)
    {
        byte[] decompressed = new byte[originalLen];

        int byteInd = 0;
        Node cur = tree;

        for (int i = 0; i < bitLen; i++)
        {
            bool bit = (bytes[i / 8] & (0x1 << (i % 8))) != 0;

            if (bit)
            {
                cur = cur.right!;
            }
            else
            {
                cur = cur.left!;
            }

            if (cur.isLeaf)
            {
                if (byteInd >= decompressed.Length)
                    break;
                decompressed[byteInd] = cur.value;
                cur = tree;
                byteInd++;
            }
        }

        return decompressed;
    }

    // * ==================================================
}

}