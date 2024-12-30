namespace OwlTree.Tests;

public class HuffmanTests
{
    [Fact]
    public void HuffmanTest()
    {
        var sendPacket = new Packet(100);
        sendPacket.header.owlTreeVer = 1;
        sendPacket.header.appVer = 14;
        sendPacket.header.timestamp = 0x1122334455;
        sendPacket.header.sender = 1;
        sendPacket.header.hash = 0x44334433;
        sendPacket.header.compressionEnabled = true;

        for (int i = 1; i < 5; i++)
        {
            var span = sendPacket.GetSpan(20);
            for (int j = 0; j < span.Length; j++)
                span[j] = (byte)i;
        }

        var bytes1 = sendPacket.GetPacket().ToArray();

        Huffman.Encode(sendPacket);

        var bytes3 = sendPacket.GetPacket().ToArray();

        Huffman.Decode(sendPacket);
        
        var bytes2 = sendPacket.GetPacket().ToArray();

        Assert.Fail("\n" + BitConverter.ToString(bytes1) + "\n\n" + BitConverter.ToString(bytes3) + "\n\n" + BitConverter.ToString(bytes2));
    }

    [Fact]
    public void QuantizationTest()
    {
        var noQuant = new Packet(6000);
        var withQuant = new Packet(6000);

        var bytes1 = noQuant.GetSpan(4 * 600);
        var bytes2 = withQuant.GetSpan(600);
        var rand = new Random();
        var floats = new float[20];
        for (int i = 0; i < floats.Length; i++)
        {
            floats[i] = rand.NextSingle();
        }

        for (int i = 0; i < bytes2.Length; i++)
        {
            var next = floats[i % floats.Length];
            BitConverter.TryWriteBytes(bytes1.Slice(i * 4), next);
            bytes2[i] = (byte)(next * 255);
        }

        var originalPacket = noQuant.GetPacket().ToArray();
        var quantPacket = withQuant.GetPacket().ToArray();

        Huffman.Encode(noQuant);
        Huffman.Encode(withQuant);

        var compressed = noQuant.GetPacket().ToArray();
        var compWithQuant = withQuant.GetPacket().ToArray();

        Assert.Fail($"Results:\n   Original: {originalPacket.Length} vs {quantPacket.Length}\n   Compress: {compressed.Length} vs {compWithQuant.Length}\n   Factor: {(float)compressed.Length / originalPacket.Length} vs {(float)compWithQuant.Length / quantPacket.Length}\n");
    }
}