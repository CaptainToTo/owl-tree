using System.Net;
using System.Net.Sockets;
using FileInitializer;
using OwlTree;

namespace Unit;

public class RudpSocket
{
    [Fact]
    public void ServerSocketOrdered()
    {
        Logs.InitPath("logs/RUDP/Ordered");
        Logs.InitFiles("logs/RUDP/Ordered/ServerPackets.log");

        var server = new RudpServerSocket(new IPEndPoint(IPAddress.Any, 0));
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Any, 0));

        server.AddEndpoint(new IPEndPoint(IPAddress.Parse("127.0.0.1"), ((IPEndPoint)socket.LocalEndPoint!).Port));
        var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), server.Port);

        Assert.True(endpoint != null, "server RUDP socket failed to bind to an endpoint.");

        var packet = new Packet(512, true);
        packet.header.timestamp = 100;
        packet.header.packetNum = 0;
        socket.SendTo(packet.GetPacket().ToArray(), endpoint);

        packet.header.timestamp = 200;
        packet.header.packetNum = 1;
        socket.SendTo(packet.GetPacket().ToArray(), endpoint);

        packet.header.timestamp = 500;
        packet.header.packetNum = 4;
        socket.SendTo(packet.GetPacket().ToArray(), endpoint);

        packet.header.timestamp = 400;
        packet.header.packetNum = 3;
        socket.SendTo(packet.GetPacket().ToArray(), endpoint);

        packet.header.timestamp = 300;
        packet.header.packetNum = 2;
        socket.SendTo(packet.GetPacket().ToArray(), endpoint);

        packet.header.timestamp = 600;
        packet.header.packetNum = 5;
        socket.SendTo(packet.GetPacket().ToArray(), endpoint);

        IPEndPoint source = new IPEndPoint(IPAddress.Any, 0);
        var buffer = new byte[512];
        var numReceived = 0;
        while (server.Available > 0)
        {
            var result = server.ReceiveFrom(buffer, ref source, out int dataLen);

            Assert.True(result == RudpResult.NewPacket, $"Received packet is not being added to the RUDP queue. Is instead treated as {result}. Packet received from {source.Address}:{source.Port}.");
            numReceived++;
        }

        Assert.True(numReceived == 6, $"Failed to receive all sent packets, only got {numReceived}, should be 6.");

        Assert.True(server.HasNextPackets, "Received packets are not registering as ready to dequeue.");
        
        var expectedPacket = 0;
        while (server.TryGetNextPacket(out var bytes, out source))
        {
            packet.Reset();
            packet.FromBytes(bytes, 0);
            Assert.True(packet.header.packetNum == expectedPacket, $"Expected packet number {expectedPacket}, got {packet.header.packetNum} instead.");
            expectedPacket++;

            File.AppendAllText("logs/RUDP/Ordered/ServerPackets.log", BitConverter.ToString(bytes) + "\n\n\n");
        }

        Assert.True(expectedPacket == 6, $"Failed to dequeue all received packets, only got {expectedPacket}, should be 6.");
    }

    [Fact]
    public void ClientSocketOrdered()
    {
        Logs.InitPath("logs/RUDP/Ordered");
        Logs.InitFiles("logs/RUDP/Ordered/ClientPackets.log");

        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Any, 0));
        var client = new RudpClientSocket(new IPEndPoint(IPAddress.Any, 0), new IPEndPoint(IPAddress.Parse("127.0.0.1"), ((IPEndPoint)socket.LocalEndPoint!).Port));

        var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), client.Port);

        Assert.True(endpoint != null, "server RUDP socket failed to bind to an endpoint.");

        var packet = new Packet(512, true);
        packet.header.timestamp = 100;
        packet.header.packetNum = 0;
        socket.SendTo(packet.GetPacket().ToArray(), endpoint);

        packet.header.timestamp = 200;
        packet.header.packetNum = 1;
        socket.SendTo(packet.GetPacket().ToArray(), endpoint);

        packet.header.timestamp = 500;
        packet.header.packetNum = 4;
        socket.SendTo(packet.GetPacket().ToArray(), endpoint);

        packet.header.timestamp = 400;
        packet.header.packetNum = 3;
        socket.SendTo(packet.GetPacket().ToArray(), endpoint);

        packet.header.timestamp = 300;
        packet.header.packetNum = 2;
        socket.SendTo(packet.GetPacket().ToArray(), endpoint);

        packet.header.timestamp = 600;
        packet.header.packetNum = 5;
        socket.SendTo(packet.GetPacket().ToArray(), endpoint);

        IPEndPoint source = new IPEndPoint(IPAddress.Any, 0);
        var buffer = new byte[512];
        var numReceived = 0;
        while (client.Available > 0)
        {
            var result = client.ReceiveFrom(buffer, ref source, out int dataLen);

            Assert.True(result == RudpResult.NewPacket, $"Received packet is not being added to the RUDP queue. Is instead treated as {result}. Packet received from {source.Address}:{source.Port}.");
            numReceived++;
        }

        Assert.True(numReceived == 6, $"Failed to receive all sent packets, only got {numReceived}, should be 6.");

        Assert.True(client.NextPacketReady, "Received packets are not registering as ready to dequeue.");
        
        var expectedPacket = 0;
        while (client.TryGetNextPacket(out var bytes, out source))
        {
            packet.Reset();
            packet.FromBytes(bytes, 0);
            Assert.True(packet.header.packetNum == expectedPacket, $"Expected packet number {expectedPacket}, got {packet.header.packetNum} instead.");
            expectedPacket++;

            File.AppendAllText("logs/RUDP/Ordered/ClientPackets.log", BitConverter.ToString(bytes) + "\n\n\n");
        }

        Assert.True(expectedPacket == 6, $"Failed to dequeue all received packets, only got {expectedPacket}, should be 6.");
    }
}
