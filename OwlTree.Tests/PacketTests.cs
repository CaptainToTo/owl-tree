namespace OwlTree.Tests;

public class PacketTests
{
    [Fact]
    public void HeaderTest()
    {
        var packet = new Packet(50);
        
        packet.header.owlTreeVer = 1;
        packet.header.appVer = 14;
        packet.header.compressionEnabled = true;
        packet.header.flag2 = true;
        packet.header.timestamp = 0x1122334455;
        packet.header.sender = 1;
        packet.header.hash = 0x44334433;

        var span = packet.GetSpan(10);
        for (int i = 0; i < span.Length; i++)
        {
            span[i] = 0xee;
        }

        var final = packet.GetPacket();

        var packet2 = new Packet(50);
        packet2.FromBytes(final.ToArray(), 0);

        Assert.True(packet2.header.owlTreeVer == packet.header.owlTreeVer, 
            "owl tree ver mismatch (r, s): " + packet2.header.owlTreeVer + ", " + packet.header.owlTreeVer);
        Assert.True(packet2.header.appVer == packet.header.appVer, 
            "app ver mismatch (r, s): " + packet2.header.appVer + ", " + packet.header.appVer);
        Assert.True(packet2.header.compressionEnabled == packet.header.compressionEnabled, 
            "compress mismatch (r, s): " + packet2.header.compressionEnabled + ", " + packet.header.compressionEnabled);
        Assert.True(packet2.header.flag2 == packet.header.flag2, 
            "flag2 mismatch (r, s): " + packet2.header.flag2 + ", " + packet.header.flag2);
        Assert.True(packet2.header.timestamp == packet.header.timestamp, 
            "timestamp mismatch (r, s): " + packet2.header.timestamp + ", " + packet.header.timestamp);
        Assert.True(packet2.header.length == packet.header.length, 
            "length mismatch (r, s): " + packet2.header.length + ", " + packet.header.length);
        Assert.True(packet2.header.sender == packet.header.sender, 
            "sender mismatch (r, s): " + packet2.header.sender + ", " + packet.header.sender);
        Assert.True(packet2.header.hash == packet.header.hash, 
            "hash mismatch (r, s): " + packet2.header.hash + ", " + packet.header.hash);
        
        packet2.StartMessageRead();
        Assert.True(packet2.TryGetNextMessage(out var message));

        Assert.True(message.Length == span.Length);
    }

    [Fact]
    public void NoFragmentTest()
    {
        var sendPacket = new Packet(200);
        sendPacket.header.owlTreeVer = 1;
        sendPacket.header.appVer = 14;
        sendPacket.header.compressionEnabled = true;
        sendPacket.header.flag2 = true;
        sendPacket.header.timestamp = 0x1122334455;
        sendPacket.header.sender = 1;
        sendPacket.header.hash = 0x44334433;

        for (int i = 1; i < 6; i++)
        {
            var span = sendPacket.GetSpan(20);
            for (int j = 0; j < span.Length; j++)
                span[j] = (byte)i;
        }

        var final = sendPacket.GetPacket();

        var readPacket = new Packet(50);

        int iters = 0;
        do {
            var readBuffer = final.Slice(iters * 50, Math.Min(final.Length - (iters * 50), 50)).ToArray();
            readPacket.FromBytes(readBuffer, 0);
            iters++;
        } while (readPacket.Incomplete);

        readPacket.StartMessageRead();
        sendPacket.StartMessageRead();
        int lastMes = 0;
        while (readPacket.TryGetNextMessage(out var mes1) && sendPacket.TryGetNextMessage(out var mes2))
        {
            Assert.True(mes1[0] == mes2[0], "message mismatch (r, s): " + mes1[0] + ", " + mes2[0]);
            lastMes = mes1[0];
        }

        var read = readPacket.GetPacket();

        Assert.Fail(BitConverter.ToString(final.ToArray()) + "\n\n" + BitConverter.ToString(read.ToArray()) +
            "\n\n" + lastMes);
    }

    [Fact]
    public void FragmentTest()
    {
        var sendPacket = new Packet(100, true);
        sendPacket.header.owlTreeVer = 1;
        sendPacket.header.appVer = 14;
        sendPacket.header.compressionEnabled = true;
        sendPacket.header.flag2 = true;
        sendPacket.header.timestamp = 0x1122334455;
        sendPacket.header.sender = 1;
        sendPacket.header.hash = 0x44334433;

        for (int i = 1; i < 8; i++)
        {
            var span = sendPacket.GetSpan(20);
            for (int j = 0; j < span.Length; j++)
                span[j] = (byte)i;
        }

        var final = sendPacket.GetPacket().ToArray();
        sendPacket.Reset();
        var final2 = sendPacket.GetPacket().ToArray();
        sendPacket.Reset();
        var final3 = sendPacket.GetPacket().ToArray();

        // Assert.Fail(BitConverter.ToString(final) + "\n\n" + BitConverter.ToString(final2) + "\n\n" + BitConverter.ToString(final3));

        var readPacket = new Packet(50);

        int iters = 0;
        do {
            var readBuffer = final;
            readPacket.FromBytes(readBuffer, 0);
            iters++;
        } while (readPacket.Incomplete);

        readPacket.StartMessageRead();
        sendPacket.StartMessageRead();
        int lastMes = 0;
        while (readPacket.TryGetNextMessage(out var mes1))
        {
            lastMes = mes1[0];
        }

        var read = readPacket.GetPacket();

        Assert.Fail(BitConverter.ToString(final.ToArray()) + "\n\n" + BitConverter.ToString(read.ToArray()) +
            "\n\n" + lastMes);
    }
}