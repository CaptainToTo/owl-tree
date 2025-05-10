using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Priority_Queue;

namespace OwlTree
{
    /// <summary>
    /// Result value of receiving packets on an RUDP socket.
    /// </summary>
    public enum RudpResult
    {
        /// <summary>
        /// The receive failed.
        /// </summary>
        Failed,
        /// <summary>
        /// A remote endpoint requested a packet to be resent.
        /// </summary>
        ResendRequest,
        /// <summary>
        /// An unregistered endpoint sent a packet, handle as if using normal UDP.
        /// </summary>
        UnregisteredEndpoint,
        /// <summary>
        /// A registered endpoint sent a new packet.
        /// </summary>
        NewPacket,
        PingRequest
    }

    internal class EndpointData
    {
        public readonly IPEndPoint Endpoint;
        // the next packet this socket will send to this endpoint
        private uint _nextOutgoingPacketNum;
        // the next packet expected to be received based on the last packet received
        private uint _expectedPacketNum;
        // the next packet that should be provided to the program
        private uint _nextIncomingPacketNum;

        private long _latency;

        // packet cache
        private SimplePriorityQueue<byte[], uint> _incoming = new();
        private List<(uint packetNum, long timestamp)> _missingPackets = new();
        private byte[][] _sentPackets;

        /// <summary>
        /// Returns true if this endpoint is missing packets from the expected order.
        /// </summary>
        public bool IsMissingPackets => _missingPackets.Count > 0;

        /// <summary>
        /// Returns true if the next packet has been received.
        /// </summary>
        public bool NextPacketReady => _incoming.Count > 0 && _incoming.GetPriority(_incoming.First) <= _nextIncomingPacketNum;

        /// <summary>
        /// Iterable of missing packets. Over time, this clears itself as packets expire.
        /// </summary>
        public IEnumerable<uint> MissingPackets => _missingPackets.Select(a => a.packetNum);

        public EndpointData(IPEndPoint endpoint, int sendRecordSize)
        {
            Endpoint = endpoint;
            _sentPackets = new byte[sendRecordSize][];
        }

        /// <summary>
        /// Removes missing packets that have expired. They are too old to keep requesting.
        /// </summary>
        public void ClearExpiredMissingPackets()
        {
            if (_missingPackets.Count == 0)
                return;

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var cutOff = _latency * 3;
            for (int i = 0; i < _missingPackets.Count; i++)
            {
                if (now - _missingPackets[i].timestamp > cutOff)
                {
                    if (_missingPackets[i].packetNum >= _nextIncomingPacketNum)
                        _nextIncomingPacketNum = _missingPackets[i].packetNum + 1;
                    _missingPackets.RemoveAt(i);
                    i--;
                }
            }
        }

        public void AddIncomingPacket(byte[] bytes, uint packetNum, long sentAt)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _latency = timestamp - sentAt;
            if (_expectedPacketNum <= packetNum)
            {
                _incoming.Enqueue(bytes, packetNum);

                if (packetNum > _expectedPacketNum)
                {
                    for (uint i = _expectedPacketNum; i < packetNum; i++)
                        _missingPackets.Add((i, timestamp));
                }

                _expectedPacketNum = packetNum + 1;
            }
            else if (MissingPackets.Contains(packetNum))
            {
                _missingPackets.RemoveAt(_missingPackets.FindIndex(a => a.packetNum == packetNum));
                _incoming.Enqueue(bytes, packetNum);
            }
        }

        public bool TryGetNextPacket(out byte[] bytes, out uint packetNum)
        {
            if (!NextPacketReady)
            {
                bytes = null;
                packetNum = 0;
                return false;
            }

            packetNum = _incoming.GetPriority(_incoming.First);
            bytes = _incoming.Dequeue();
            _nextIncomingPacketNum = Math.Max(packetNum + 1, _nextIncomingPacketNum);
            return true;
        }

        public uint AddSentPacket(byte[] bytes)
        {
            _sentPackets[_nextOutgoingPacketNum % _sentPackets.Length] = bytes;
            var ind = _nextOutgoingPacketNum;
            _nextOutgoingPacketNum++;
            return ind;
        }

        public byte[] GetSentPacket(uint packetNum)
        {
            if (packetNum > _nextOutgoingPacketNum || _nextOutgoingPacketNum - packetNum > _sentPackets.Length)
                return null;
            return _sentPackets[packetNum % _sentPackets.Length];
        }

        public static bool operator ==(EndpointData a, IPEndPoint b) => (a is null && b is null) || (a is not null && b is not null && a.Endpoint.Address.Equals(b.Address) && a.Endpoint.Port == b.Port);
        public static bool operator !=(EndpointData a, IPEndPoint b) => a is null || b is null || !a.Endpoint.Address.Equals(b.Address) || a.Endpoint.Port != b.Port;

        public override bool Equals(object obj) => obj != null && obj.GetType() == typeof(EndpointData) && ((EndpointData)obj == this);
        public override int GetHashCode() => base.GetHashCode();
    }

    public class RudpClientSocket
    {
        /// <summary>
        /// The socket this object wraps.
        /// </summary>
        public readonly Socket Socket;

        /// <summary>
        /// The port this socket is bound to.
        /// </summary>
        public int Port => ((IPEndPoint)Socket.LocalEndPoint).Port;

        /// <summary>
        /// The number of bytes of data received from the network and available to be read.
        /// </summary>
        public int Available => Socket.Available;

        /// <summary>
        /// Whether the socket has packets ready to be processed.
        /// </summary>
        public bool NextPacketReady => _endpoint.NextPacketReady;

        private byte[] _resendRequest;
        
        private EndpointData _endpoint;

        /// <summary>
        /// Create a new RUDP socket, and bind to the given endpoint.
        /// This is a client socket, meaning packets will only be managed with RUDP between the given remote endpoint.
        /// </summary>
        public RudpClientSocket(IPEndPoint localEndpoint, IPEndPoint remoteEndpoint)
        {
            _endpoint = new EndpointData(remoteEndpoint, 32);
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Socket.Bind(localEndpoint);
            _resendRequest = new byte[Packet.Header.ByteLength + 1];
        }

        /// <summary>
        /// Receive any new data from the socket.
        /// If the source endpoint isn't the target remote endpoint, then the packet will treated as normal UDP packet.
        /// </summary>
        public RudpResult ReceiveFrom(byte[] buffer, ref IPEndPoint source, out int dataLen)
        {
            var s = (EndPoint)source;
            dataLen = Socket.ReceiveFrom(buffer, ref s);
            source = (IPEndPoint)s;

            if (dataLen <= 0)
                return RudpResult.Failed;

            if (_endpoint != source)
                return RudpResult.UnregisteredEndpoint;

            var header = new Packet.Header();
            header.FromBytes(buffer);

            var timestamp = header.timestamp;
            var packetNum = header.packetNum;
            var isResendRequest = header.resendRequest;
            var length = header.length;

            if (isResendRequest)
            {
                var bytes = _endpoint.GetSentPacket(packetNum);
                if (bytes != null)
                    Socket.SendTo(bytes, _endpoint.Endpoint);
                return RudpResult.ResendRequest;
            }

            _endpoint.AddIncomingPacket(buffer.AsSpan(0, length).ToArray(), packetNum, timestamp);

            return RudpResult.NewPacket;
        }

        public void RequestMissingPackets()
        {
            _endpoint.ClearExpiredMissingPackets();
            if (_endpoint.IsMissingPackets)
            {
                var header = new Packet.Header();
                header.resendRequest = true;
                header.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                header.length = Packet.Header.ByteLength;
                foreach (var missing in _endpoint.MissingPackets)
                {
                    header.packetNum = missing;
                    header.InsertBytes(_resendRequest);
                    Socket.SendTo(_resendRequest, _endpoint.Endpoint);
                }
            }
        }

        /// <summary>
        /// Try to get the next ordered packet from the remote endpoint.
        /// </summary>
        public bool TryGetNextPacket(out byte[] bytes, out IPEndPoint remoteEndpoint)
        {
            remoteEndpoint = _endpoint.Endpoint;
            return _endpoint.TryGetNextPacket(out bytes, out var packetNum);
        }

        /// <summary>
        /// Send the given packet to the given endpoint. If the endpoint isn't the target remote endpoint, 
        /// this will be treated as a normal UDP packet.
        /// </summary>
        public int SendTo(byte[] bytes, IPEndPoint endpoint)
        {
            if (_endpoint != endpoint)
                return Socket.SendTo(bytes, endpoint);

            var header = new Packet.Header();
            header.FromBytes(bytes);
            header.packetNum = _endpoint.AddSentPacket(bytes);
            header.InsertBytes(bytes);

            return Socket.SendTo(bytes, endpoint);
        }

        /// <summary>
        /// Close the socket.
        /// </summary>
        public void Close()
        {
            Socket.Close();
        }
    }

    /// <summary>
    /// An network socket wrapper that implements RUDP. The server socket will 
    /// manage packets between multiple remote endpoints.
    /// </summary>
    public class RudpServerSocket
    {
        /// <summary>
        /// The socket this object wraps.
        /// </summary>
        public readonly Socket Socket;

        /// <summary>
        /// The port this socket is bound to.
        /// </summary>
        public int Port => ((IPEndPoint)Socket.LocalEndPoint).Port;

        /// <summary>
        /// The number of bytes of data received from the network and available to be read.
        /// </summary>
        public int Available => Socket.Available;

        /// <summary>
        /// Whether the socket has packets ready to be processed.
        /// </summary>
        public bool HasNextPackets => _endpoints.Any(e => e.NextPacketReady);

        private byte[] _resendRequest;

        private List<EndpointData> _endpoints = new();
        private EndpointData FindData(IPEndPoint endpoint)
        {
            foreach (var data in _endpoints)
                if (data == endpoint) return data;
            return null;
        }

        /// <summary>
        /// Iterable of endpoints this socket is managing packets for.
        /// </summary>
        public IEnumerable<IPEndPoint> Endpoints => _endpoints.Select(e => e.Endpoint);

        /// <summary>
        /// Create a new RUDP socket, and bind to the given endpoint.
        /// This is a server socket, meaning it will manage packets from multiple remote endpoints.
        /// </summary>
        public RudpServerSocket(IPEndPoint endpoint)
        {
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Socket.Bind(endpoint);
            _resendRequest = new byte[Packet.Header.ByteLength + 1];
        }

        /// <summary>
        /// Receive any new data from the socket.
        /// If the source endpoint isn't registered, then the packet will treated as normal UDP packet.
        /// </summary>
        public RudpResult ReceiveFrom(byte[] buffer, ref IPEndPoint source, out int dataLen)
        {
            var s = (EndPoint)source;
            dataLen = Socket.ReceiveFrom(buffer, ref s);
            source = (IPEndPoint)s;

            if (dataLen <= 0)
                return RudpResult.Failed;

            var data = FindData(source);
            if (data == null)
                return RudpResult.UnregisteredEndpoint;

            var header = new Packet.Header();
            header.FromBytes(buffer);

            var timestamp = header.timestamp;
            var packetNum = header.packetNum;
            var isResendRequest = header.resendRequest;
            var isPingRequest = header.pingRequest;
            var length = header.length;

            if (isPingRequest)
                return RudpResult.PingRequest;

            if (isResendRequest)
            {
                var bytes = data.GetSentPacket(packetNum);
                if (bytes != null)
                    Socket.SendTo(bytes, data.Endpoint);
                return RudpResult.ResendRequest;
            }

            data.AddIncomingPacket(buffer.AsSpan(0, length).ToArray(), packetNum, timestamp);

            return RudpResult.NewPacket;
        }

        public void RequestMissingPackets()
        {
            for (int i = 0; i < _endpoints.Count; i++)
            {
                _endpoints[i].ClearExpiredMissingPackets();
                if (_endpoints[i].IsMissingPackets)
                {
                    var header = new Packet.Header();
                    header.resendRequest = true;
                    header.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    header.length = Packet.Header.ByteLength;
                    foreach (var missing in _endpoints[i].MissingPackets)
                    {
                        header.packetNum = missing;
                        header.InsertBytes(_resendRequest);
                        Socket.SendTo(_resendRequest, _endpoints[i].Endpoint);
                    }
                }
            }
        }

        private EndpointData FindNextPacketSource(out int ind)
        {
            ind = 0;
            for (int i = 0; i < _endpoints.Count; i++)
            {
                if (_endpoints[i].NextPacketReady)
                {
                    ind = i;
                    return _endpoints[i];
                }
            }
            return null;
        }

        private void Swap(int i, int j)
        {
            var temp = _endpoints[j];
            _endpoints[j] = _endpoints[i];
            _endpoints[i] = temp;
        }
        
        /// <summary>
        /// Get the next packet from any endpoint that has its ordered, next packet ready.
        /// </summary>
        public bool TryGetNextPacket(out byte[] bytes, out IPEndPoint remoteEndpoint)
        {
            bytes = null;
            remoteEndpoint = null;
            
            var data = FindNextPacketSource(out var i);
            if (data == null)
                return false;
            
            remoteEndpoint = data.Endpoint;
            data.TryGetNextPacket(out bytes, out var packetNum);
            
            // re-order endpoints to make retrieval of next packet faster
            if (data.NextPacketReady && i != 0)
                Swap(i, 0);

            return true;
        }

        /// <summary>
        /// Send the given packet to the given endpoint. If the endpoint isn't registered, this will be treated
        /// as a normal UDP packet.
        /// </summary>
        public int SendTo(byte[] bytes, IPEndPoint endpoint)
        {
            var data = FindData(endpoint);
            if (data == null)
                return Socket.SendTo(bytes, endpoint);

            var header = new Packet.Header();
            header.FromBytes(bytes);
            header.packetNum = data.AddSentPacket(bytes);
            header.InsertBytes(bytes);

            return Socket.SendTo(bytes, endpoint);
        }

        /// <summary>
        /// Register a new remote end point to start using RUDP with.
        /// </summary>
        public void AddEndpoint(IPEndPoint endpoint)
        {
            _endpoints.Add(new EndpointData(endpoint, 32));
        }

        /// <summary>
        /// Unregister a remote endpoint to stop using RUDP with.
        /// </summary>
        public void RemoveEndpoint(IPEndPoint endpoint)
        {
            _endpoints.RemoveAt(_endpoints.FindIndex(e => e == endpoint));
        }

        /// <summary>
        /// Close the socket.
        /// </summary>
        public void Close()
        {
            Socket.Close();
        }
    }
}