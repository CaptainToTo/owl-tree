using FileInitializer;
using OwlTree;

namespace Unit;

public class PacketTests
{
    [Fact]
    public void LargeBuffer()
    {
        Logs.InitPath("logs/Packet/LargeBuffer");
        Logs.InitFiles("logs/Packet/LargeBuffer/Packets.log");

        var buffer = new byte[]{
            0x01, 0x00, 0x01, 0x00, 0x47, 0x57, 0xCD, 0xA2, 0x95, 0x01, 0x00, 0x00, 0x4D, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x38, 0x00, 0x00, 0x00,
            0x8A, 0x00, 0x00, 0x00, 0x0E, 0xB0, 0xB2, 0xA8, 0xCD, 0x2E, 0x6C, 0x40, 0xBE, 0x7D, 0x06, 0x4C, 0x60, 0x45, 0x0C, 0x8D, 0x5E, 0x05, 0x00, 0xF6, 0xFA, 0xEC, 0x2F, 0xCD, 0xCE, 0x13, 0x82, 0xDA, 0x5E, 0x9F, 0xFF, 0x85,
            0xD9, 0x79, 0x42, 0x50, 0x03, 0x01, 0x00, 0x01, 0x00, 0xE7, 0x57, 0xCD, 0xA2, 0x95, 0x01, 0x00, 0x00, 0x70, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00,
            0x00, 0x8C, 0x00, 0x00, 0x00, 0x6A, 0x01, 0x00, 0x00, 0x14, 0xB0, 0xB2, 0xA8, 0xCD, 0xEC, 0xFB, 0xB7, 0x01, 0x02, 0x5E, 0x78, 0x45, 0xBF, 0xA6, 0x2C, 0x88, 0x65, 0x5D, 0x3B, 0x06, 0x4C, 0xB0, 0x22, 0x46, 0x00, 0xFE,
            0xF6, 0xE2, 0x5F, 0x71, 0xEB, 0x24, 0x21, 0x98, 0xFD, 0xED, 0xC5, 0x3F, 0xEB, 0x56, 0x25, 0x21, 0x98, 0xFD, 0xED, 0xF9, 0x9F, 0x71, 0xEB, 0x25, 0x21, 0x98, 0xFD, 0xED, 0xF9, 0x5F, 0x71, 0xAB, 0x92, 0x10, 0xCC, 0xFE,
            0xF6, 0xFC, 0x2F, 0xB8, 0x35, 0x5A, 0x42, 0x30, 0x03, 0x01, 0x00, 0x01, 0x00, 0x03, 0x59, 0xCD, 0xA2, 0x95, 0x01, 0x00, 0x00, 0x77, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x01, 0x00, 0x00, 0x00, 0xA8, 0x00, 0x00, 0x00, 0xAE, 0x01, 0x00, 0x00, 0x13, 0xB0, 0xB2, 0xA8, 0xCD, 0x2E, 0x6C, 0x80, 0x35, 0x03, 0x15, 0x4C, 0x88, 0x05, 0xCB, 0xB4, 0x6C, 0x3C, 0x80, 0x09, 0x2B, 0xC4, 0x88,
            0x15, 0x00, 0xEE, 0xE6, 0xEC, 0x2F, 0xDA, 0x15, 0x9E, 0x10, 0xF4, 0xEE, 0xE6, 0xEC, 0x2F, 0xD9, 0x05, 0x4F, 0x08, 0x7A, 0x77, 0x73, 0xF6, 0x97, 0xED, 0x2A, 0x4F, 0x08, 0x7A, 0x77, 0x73, 0xFE, 0x17, 0xED, 0x82, 0x27,
            0x04, 0xBD, 0xBB, 0x39, 0xFF, 0x4B, 0x76, 0xA5, 0x27, 0x04, 0xBD, 0xBB, 0x39, 0xFF, 0xCB, 0x76, 0xB5, 0x27, 0x04, 0x3D
        };

        var ReadPacket = new Packet(2042);
        int dataRemaining = -1;
        int dataLen = -1;

        int packetCount = 0;
        int packet1Len = 77;
        int packet2Len = 112;
        int packet3Len = 119;

        do
        {
            ReadPacket.Clear();

            int iters = 0;
            do
            {
                try
                {
                    if (dataRemaining <= 0)
                    {
                        dataLen = buffer.Length;
                        dataRemaining = dataLen;
                    }
                    dataRemaining -= ReadPacket.FromBytes(buffer, dataLen - dataRemaining, dataLen);
                    iters++;
                }
                catch
                {
                    dataLen = -1;
                    break;
                }
            } while (ReadPacket.Incomplete && iters < 10);

            File.AppendAllText("logs/Packet/LargeBuffer/Packets.log", ReadPacket.ToString() + "\n\n");
            var len = ReadPacket.GetPacket().Length;

            if (packetCount == 0)
                Assert.True(len == packet1Len, $"1st packet is not {packet1Len} bytes, is instead {len}");
            else if (packetCount == 1)
                Assert.True(len == packet2Len, $"2nd packet is not {packet2Len} bytes, is instead {len}");
            else if (packetCount == 2)
                Assert.True(len == packet3Len, $"2nd packet is not {packet3Len} bytes, is instead {len}");

            packetCount++;

        } while (dataRemaining > 0);

        Assert.True(packetCount == 3, $"{packetCount} packets were read, not 3");
    }

    [Fact]
    public void IncompleteHeader()
    {
        Logs.InitPath("logs/Packet/Incomplete");
        Logs.InitFiles("logs/Packet/Incomplete/Packets.log");

        var buffer1 = new byte[]{
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x3C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x18, 0x00, 0x00, 0x00, 0x45, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00
        };
        var buffer2 = new byte[]{
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x3C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x18, 0x00, 0x00, 0x00, 0x45, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        };

        var packet = new Packet(64);

        int read = packet.FromBytes(buffer1, 0, buffer1.Length);
        File.AppendAllText("logs/Packet/Incomplete/Packets.log", BitConverter.ToString(packet.GetPacket().ToArray()) + "\n\n");

        Assert.True(read == 60, "packet did not read 60 bytes, instead got " + read);

        read = packet.FromBytes(buffer1, read, buffer1.Length);

        Assert.True(read == 4, "packet did not read 4 bytes, instead got " + read);
        Assert.True(packet.Incomplete, "2nd packet is not considering itself incomplete");

        read = packet.FromBytes(buffer2, 0, buffer2.Length);
        File.AppendAllText("logs/Packet/Incomplete/Packets.log", BitConverter.ToString(packet.GetPacket().ToArray()) + "\n\n");

        Assert.True(read == 56, "packet did not read 56 bytes, instead got " + read);
        Assert.True(!packet.Incomplete, "packet is still considered incomplete despite getting all data.");

    }

    [Fact]
    public void PacketFragment()
    {
        Logs.InitPath("logs/Packet/Fragment");
        Logs.InitFiles("logs/Packet/Fragment/Packets.log");

        var buffer = new byte[75];
        var header = new Fragment.Header();
        header.fragment = 1;
        header.start = 100;
        header.timestamp = 500;
        header.packetNum = 5;
        header.length = 50;

        header.InsertBytes(buffer);
        File.AppendAllText("logs/Packet/Fragment/Packets.log", BitConverter.ToString(buffer) + "\n\n");

        var decode = new Fragment.Header();
        decode.FromBytes(buffer);


        Assert.True(decode.fragment == header.fragment, "fragment decoded incorrectly, got " + decode.fragment);
        Assert.True(decode.start == header.start, "start decoded incorrectly, got " + decode.start);
        Assert.True(decode.timestamp == header.timestamp, "timestamp decoded incorrectly, got " + decode.timestamp);
        Assert.True(decode.packetNum == header.packetNum, "packetNum decoded incorrectly, got " + decode.packetNum);
        Assert.True(decode.length == header.length, "length decoded incorrectly, got " + decode.length);
    }

    [Fact]
    public void ResendRequestHeader()
    {
        Logs.InitPath("logs/Packet/Resend");
        Logs.InitFiles("logs/Packet/Resend/Packets.log");

        var buffer = new byte[75];
        var header = new ResendRequest.Header();
        header.fragmentsStart = 40;
        header.hash = 0;
        header.length = 100;
        header.timestamp = 500;

        header.InsertBytes(buffer);
        File.AppendAllText("logs/Packet/Resend/Packets.log", BitConverter.ToString(buffer) + "\n\n");

        var decode = new ResendRequest.Header();
        decode.FromBytes(buffer);

        Assert.True(decode.fragmentsStart == header.fragmentsStart, "fragmentsStart  decoded incorrectly, got " + decode.fragmentsStart);
        Assert.True(decode.timestamp == header.timestamp, "timestamp decoded incorrectly, got " + decode.timestamp);
        Assert.True(decode.length == header.length, "length decoded incorrectly, got " + decode.length);
    }

    [Fact]
    public void ResendPackets()
    {
        Logs.InitPath("logs/Packet/Resend");
        Logs.InitFiles("logs/Packet/Resend/PacketRequest.log");

        var request = new ResendRequest();

        var missingPackets = new uint[] { 3, 4, 7 };
        var missingFrags = new (uint packetNum, byte fragment)[] { (5, 1), (5, 2), (6, 3), (6, 4) };

        var bytes = request.GetRequest(missingPackets, missingFrags).ToArray();

        File.AppendAllText("logs/Packet/Resend/PacketRequest.log", BitConverter.ToString(bytes) + "\n\n");

        var fragmentsStartCorrect = ResendRequest.Header.ByteLength + 12;
        var packetLengthCorrect = ResendRequest.Header.ByteLength + 12 + 20;
        var header = new ResendRequest.Header();
        header.FromBytes(bytes);

        Assert.True(header.fragmentsStart == fragmentsStartCorrect, "fragments start is incorrect, should be " + fragmentsStartCorrect + ", but got " + header.fragmentsStart);
        Assert.True(header.length == packetLengthCorrect, "resend request has an incorrect length, should be " + packetLengthCorrect + ", but got " + header.length);

        var decodedPacketNums = ResendRequest.GetPacketNums(bytes, header.fragmentsStart).ToArray();

        var str = "decoded packet nums: ";
        foreach (var n in decodedPacketNums)
            str += n.ToString() + " , ";

        File.AppendAllText("logs/Packet/Resend/PacketRequest.log", str + "\n\n");

        Assert.True(missingPackets.Length == decodedPacketNums.Length, "Did not decode the same number of missing packets as was encoded, got " + decodedPacketNums.Length);

        for (int i = 0; i < missingPackets.Length; i++)
            Assert.True(missingPackets[i] == decodedPacketNums[i], "incorrect packet number, expected " + missingPackets[i] + ", but got " + decodedPacketNums[i]);

        var decodedFragments = ResendRequest.GetFragments(bytes, header.fragmentsStart, header.length).ToArray();

        str = "decoded fragment nums: ";
        foreach (var n in decodedFragments)
            str += $"({n.packetNum}, {n.fragment}) , ";
        File.AppendAllText("logs/Packet/Resend/PacketRequest.log", str + "\n\n");

        Assert.True(missingFrags.Length == decodedFragments.Length, "Did not decode the same number of missing fragments as was encoded, got " + decodedFragments.Length);

        for (int i = 0; i < missingFrags.Length; i++)
            Assert.True(missingFrags[i] == decodedFragments[i], $"incorrect fragment, expected ({missingFrags[i].packetNum}, {missingFrags[i].fragment}), but got ({decodedFragments[i].packetNum}, {decodedFragments[i].fragment})");
    }
}