
using System;
using System.Collections.Generic;
using Priority_Queue;

namespace OwlTree
{

public static class Huffman
{
    internal struct ByteEncoding
    {
        public byte value;
        public byte encoding;
        public int bitLen;

        public ByteEncoding(int _bitLen = -1) {
            value = 0;
            encoding = 0;
            bitLen = _bitLen;
        }

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
        public Node left;
        public Node right;
        public Node parent;

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

        private static bool RecurseEquals(Node a, Node b)
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

        public void Encode(Span<byte> bytes, ref int bitInd)
        {
            var ind = (int)(bitInd / 8);
            if (isLeaf)
            {
                bytes[ind] |= (byte)(0x1 << (bitInd % 8));
                bitInd++;

                for (int i = 0; i < 8; i++)
                {
                    if (bitInd % 8 == 0) ind++;
                    bool bit = (byte)((value >> i) & 0x1) == 1;
                    if (bit)
                        bytes[ind] |= (byte)((0x1) << (bitInd % 8));
                    else
                        bytes[ind] &= (byte)(~(0x1 << (bitInd % 8)));
                    bitInd++;
                }
            }
            else
            {
                bytes[ind] &= (byte)(~(0x1 << (bitInd % 8)));
                bitInd++;
                if (left != null)
                    left.Encode(bytes, ref bitInd);
                
                if (right != null)
                    right.Encode(bytes, ref bitInd);
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
    public static void Encode(Packet packet)
    {
        var bytes = packet.GetMessages();
        var histogram = BuildHistogram(bytes, out var unique);
        var tree = BuildEncodingTree(histogram);
        var table = new ByteEncoding[byte.MaxValue + 1];
        BuildEncodingTable(table, tree);
        var compression = Compress(bytes, table, out var bitLen);

        var size = tree.Size();
        if (bytes.Length < 13 + (size * 2) + (bitLen / 8) + 1)
            return;

        packet.header.compressionEnabled = true;
        BitConverter.TryWriteBytes(bytes, bytes.Length);
        BitConverter.TryWriteBytes(bytes.Slice(4), bitLen);
        bytes[8] = (byte) unique;
        int treeInd = 9;
        int treeBitInd = treeInd * 8;
        tree.Encode(bytes, ref treeBitInd);
        treeInd = (treeBitInd / 8) + 1;
        for (int i = 0; i < (bitLen / 8) + 1; i++)
        {
            var b = compression[i];
            bytes[treeInd] = b;
            treeInd++;
        }
        packet.SetSize(treeInd);
        return;
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

        SimplePriorityQueue<Node, int> q = new SimplePriorityQueue<Node, int>();
        for (int i = 0; i < histogram.Length; i++)
        {
            if (histogram[i] > 0)
                q.Enqueue(new Node((byte)i, histogram[i]), histogram[i]);
        }

        while (q.Count > 1)
        {
            Node a = q.Dequeue();
            Node b = q.Count > 0 ? q.Dequeue() : null;
            var parent = new Node(0, a.prob + (b?.prob ?? 0), false);
            parent.left = a;
            parent.right = b;
            q.Enqueue(parent, parent.prob);
        }

        root = q.Dequeue();

        return root;
    }

    internal static void BuildEncodingTable(ByteEncoding[] table, Node tree, byte encoding=0, int bitLen=0)
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
    public static void Decode(Packet packet)
    {
        if (!packet.header.compressionEnabled)
        {
            return;
        }

        var bytes = packet.GetBuffer();

        var originalLen = BitConverter.ToInt32(bytes);
        var bitLen = BitConverter.ToInt32(bytes.Slice(4));
        var size = bytes[8];

        if (originalLen > bytes.Length)
        {
            return;
        }
        
        Node tree = RebuildTree(bytes.Slice(9), size, out var start);
        var decompressed = Decompress(bytes.Slice(9 + start), tree, originalLen, bitLen);

        for (int i = 0; i < decompressed.Length; i++)
        {
            bytes[i] = decompressed[i];
        }

        packet.SetSize(originalLen);

        return;
    }

    internal static Node RebuildTree(Span<byte> bytes, int size, out int last)
    {
        Node root = new Node(0, 0, false);

        Node cur = root;
        int curBit = 1;
        int curNode = 1;
        do {
            int curByte = (int)(curBit / 8);
            int bit = curBit % 8;
            if ((bytes[curByte] & (byte)(0x1 << bit)) == 0)
            {
                cur = cur.AddChild(0, 0, false);
                curBit += 1;
            }
            else
            {
                curBit += 1;
                byte value = 0;
                for (int i = 0; i < 8; i++)
                {
                    if (curBit % 8 == 0) curByte++;
                    value |= (byte)((bytes[curByte] >> (curBit % 8)) << i);
                    curBit++;
                }
                cur.AddChild(value, 0);
                while (cur.right != null && cur.parent != null)
                {
                    cur = cur.parent;
                }
                curNode++;
            }

        } while (curNode <= size);
        last = (curBit / 8) + 1;

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