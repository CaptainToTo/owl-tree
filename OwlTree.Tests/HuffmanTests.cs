using System.Text;

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
}