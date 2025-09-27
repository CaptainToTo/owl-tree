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
        PingRequest,
        NewFragment
    }

    internal class EndpointData
    {
        private struct Assembler
        {
            private byte[] _bytes;
            public readonly uint packetNum;
            public readonly long timestamp;
            private bool[] _fragments;

            public byte[] GetBytes() => _bytes;

            public Assembler(byte[] bytes, uint packetNum, byte fragments, long timestamp)
            {
                _bytes = bytes;
                this.packetNum = packetNum;
                this.timestamp = timestamp;
                _fragments = new bool[fragments];
            }

            public IEnumerable<byte> MissingFragments()
            {
                for (byte i = 0; i < _fragments.Length; i++)
                {
                    if (!_fragments[i])
                        yield return i;
                }
            }

            public bool IsComplete()
            {
                return _fragments.All(f => f);
            }

            public bool IsFragment(byte fragment)
            {
                return 0 <= fragment && fragment < _fragments.Length;
            }

            public bool HasFragment(byte fragment)
            {
                return IsFragment(fragment) && _fragments[fragment];
            }

            public void AddFragment(ReadOnlySpan<byte> bytes, byte fragment, int start)
            {
                if (!IsFragment(fragment) || HasFragment(fragment))
                    return;

                if (start + bytes.Length > _bytes.Length)
                    Array.Resize(ref _bytes, start + bytes.Length);

                for (int i = 0; i < bytes.Length; i++)
                    _bytes[i + start] = bytes[i];

                _fragments[fragment] = true;
            }
        }

        public readonly IPEndPoint Endpoint;
        // the next packet this socket will send to this endpoint
        private uint _nextOutgoingPacketNum;
        // the next packet expected to be received based on the last packet received
        private uint _expectedPacketNum;
        // the last packet that was received completely (including all fragments), which will be sent as acknowledged
        private uint _lastAcknowledgedNum;
        // the next packet that should be provided to the program
        private uint _nextIncomingPacketNum;

        private long _latency;

        public long Latency => _latency;

        public void UpdateLatency(long sentAt)
        {
            _latency = Timestamp.Now - sentAt;
        }

        // packet cache
        private SimplePriorityQueue<byte[], uint> _incoming = new();
        private Dictionary<uint, Assembler> _incomplete = new();
        private List<(uint packetNum, long timestamp)> _missingPackets = new();
        private byte[][] _sentPackets;

        /// <summary>
        /// Returns true if this endpoint is missing packets from the expected order.
        /// </summary>
        public bool IsMissingPackets => _missingPackets.Count > 0;

        public bool IsMissingFragments => _incomplete.Count > 0;

        /// <summary>
        /// Returns true if the next packet has been received.
        /// </summary>
        public bool NextPacketReady => _incoming.Count > 0 && _incoming.GetPriority(_incoming.First) <= _nextIncomingPacketNum;

        /// <summary>
        /// Iterable of missing packets. Over time, this clears itself as packets expire.
        /// </summary>
        public IEnumerable<uint> MissingPackets => _missingPackets.Select(a => a.packetNum);

        public IEnumerable<(uint packetNum, byte fragment)> MissingFragments => _incomplete.SelectMany(p => p.Value.MissingFragments().Select(f => (p.Value.packetNum, f)));

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
            var now = Timestamp.Now;
            var cutOff = _latency * 3;

            if (_missingPackets.Count > 0)
            {
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

            if (_incomplete.Count > 0)
            {
                foreach (var p in _incomplete.Keys.ToArray())
                {
                    if (now - _incomplete[p].timestamp > cutOff)
                        _incomplete.Remove(p);
                }
            }
        }

        public void AddIncomingPacket(byte[] bytes, uint packetNum, bool isFragmented, byte fragments)
        {
            var timestamp = Timestamp.Now;
            if (_expectedPacketNum <= packetNum)
            {
                if (isFragmented)
                    _incomplete.Add(packetNum, new Assembler(bytes, packetNum, fragments, timestamp));
                else
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

            if (!isFragmented && packetNum > _lastAcknowledgedNum)
                _lastAcknowledgedNum = packetNum;
        }

        public void AddIncomingFragment(ReadOnlySpan<byte> bytes, uint packetNum, byte fragment, int start)
        {
            if (!_incomplete.TryGetValue(packetNum, out var assembler))
                return;

            if (!assembler.IsFragment(fragment) || assembler.HasFragment(fragment))
                return;

            assembler.AddFragment(bytes.Slice(Fragment.Header.ByteLength), fragment, start);

            if (assembler.IsComplete())
            {
                _incoming.Enqueue(assembler.GetBytes(), assembler.packetNum);
                _incomplete.Remove(packetNum);

                if (packetNum > _lastAcknowledgedNum)
                    _lastAcknowledgedNum = packetNum;
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
            var header = new Packet.Header();
            header.FromBytes(bytes);
            header.packetNum = _nextOutgoingPacketNum;
            header.acknowledged = _lastAcknowledgedNum;
            header.fragmented = bytes.Length > Packet.MaxTransmissionUnit;
            header.fragments = (byte)MathF.Ceiling((float)bytes.Length / Packet.MaxTransmissionUnit);
            header.InsertBytes(bytes);
            _nextOutgoingPacketNum++;
            return header.packetNum;
        }

        public IEnumerable<byte[]> GetFragments(uint packetNum)
        {
            var packet = GetSentPacket(packetNum);
            if (packet == null)
                yield break;

            int start = 0;
            int length = Packet.MaxTransmissionUnit;

            // first fragment is the packet start, which should already have a complete header
            if (packet.Length <= length)
            {
                var temp = new Packet.Header();
                temp.FromBytes(packet);
                temp.acknowledged = _lastAcknowledgedNum;
                temp.timestamp = Timestamp.Now;
                temp.InsertBytes(packet);
                yield return packet;
            }
            else
                yield return packet.AsSpan(0, length).ToArray();
            start += length;

            // following fragments use fragment header
            byte fragmentNum = 1;
            var header = new Fragment.Header();
            header.packetNum = packetNum;
            while (start < packet.Length)
            {
                var data = packet.AsSpan(start, Math.Min(packet.Length - start, length - Fragment.Header.ByteLength));
                var fragment = new byte[Fragment.Header.ByteLength + data.Length];
                header.start = start;
                header.length = fragment.Length;
                header.fragment = fragmentNum;

                header.InsertBytes(fragment);
                for (int i = 0; i < data.Length; i++)
                    fragment[i + Fragment.Header.ByteLength] = data[i];

                yield return fragment;

                start += length;
                fragmentNum += 1;
            }
        }

        public byte[] GetFragment(uint packetNum, byte fragmentNum)
        {
            var packet = GetSentPacket(packetNum);
            if (packet == null)
                return null;

            var tempHeader = new Packet.Header();
            tempHeader.FromBytes(packet);
            if (fragmentNum < 1 || tempHeader.fragments <= fragmentNum)
                return null;

            var start = Packet.MaxTransmissionUnit * fragmentNum;
            var length = Packet.MaxTransmissionUnit - Fragment.Header.ByteLength;
            var data = packet.AsSpan(start, Math.Min(packet.Length - start, length));
            var fragment = new byte[Fragment.Header.ByteLength + data.Length];
            var header = new Fragment.Header();
            header.start = start;
            header.length = fragment.Length;
            header.packetNum = packetNum;
            header.fragment = fragmentNum;
            header.InsertBytes(fragment);
            for (int i = 0; i < data.Length; i++)
                fragment[i + Fragment.Header.ByteLength] = data[i];

            return fragment;
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

        private ResendRequest _resendRequest;
        
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
            _resendRequest = new ResendRequest();
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

            if (PacketType.IsFragment(buffer[0]))
            {
                var header = new Fragment.Header();
                header.FromBytes(buffer);

                _endpoint.UpdateLatency(header.timestamp);
                var packetNum = header.packetNum;
                var start = header.start;
                var length = header.length;
                var fragmentNum = header.fragment;

                _endpoint.AddIncomingFragment(buffer.AsSpan(0, length), packetNum, fragmentNum, start);

                return RudpResult.NewFragment;
            }
            else if (PacketType.IsResendRequest(buffer[0]))
            {
                var header = new ResendRequest.Header();
                header.FromBytes(buffer);

                _endpoint.UpdateLatency(header.timestamp);
                var length = header.length;
                var fragmentsStart = header.fragmentsStart;

                foreach (var p in ResendRequest.GetPacketNums(buffer, fragmentsStart))
                {
                    foreach (var bytes in _endpoint.GetFragments(p))
                        Socket.SendTo(bytes, _endpoint.Endpoint);
                }

                foreach (var p in ResendRequest.GetFragments(buffer, fragmentsStart, length))
                {
                    var bytes = _endpoint.GetFragment(p.packetNum, p.fragment);
                    Socket.SendTo(bytes, _endpoint.Endpoint);
                }

                return RudpResult.ResendRequest;
            }
            else
            {
                var header = new Packet.Header();
                header.FromBytes(buffer);

                _endpoint.UpdateLatency(header.timestamp);
                var packetNum = header.packetNum;
                var isPingRequest = header.pingRequest;
                var length = header.length;
                var fragmented = header.fragmented;
                var fragments = header.fragments;

                if (isPingRequest)
                    return RudpResult.PingRequest;

                _endpoint.AddIncomingPacket(buffer.AsSpan(0, length).ToArray(), packetNum, fragmented, fragments);

                return RudpResult.NewPacket;
            }
        }

        public void RequestMissingPackets()
        {
            _endpoint.ClearExpiredMissingPackets();
            if (_endpoint.IsMissingPackets || _endpoint.IsMissingFragments)
            {
                var bytes = _resendRequest.GetRequest(_endpoint.MissingPackets, _endpoint.MissingFragments);
                Socket.SendTo(bytes.ToArray(), _endpoint.Endpoint);
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

            var packetNum = _endpoint.AddSentPacket(bytes);

            foreach (var fragment in _endpoint.GetFragments(packetNum))
                Socket.SendTo(fragment, endpoint);

            return bytes.Length;
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

        private ResendRequest _resendRequest;

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
            _resendRequest = new ResendRequest();
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

            if (PacketType.IsFragment(buffer[0]))
            {
                var header = new Fragment.Header();
                header.FromBytes(buffer);

                data.UpdateLatency(header.timestamp);
                var packetNum = header.packetNum;
                var start = header.start;
                var length = header.length;
                var fragmentNum = header.fragment;

                data.AddIncomingFragment(buffer.AsSpan(0, length), packetNum, fragmentNum, start);

                return RudpResult.NewFragment;
            }
            else if (PacketType.IsResendRequest(buffer[0]))
            {
                var header = new ResendRequest.Header();
                header.FromBytes(buffer);

                data.UpdateLatency(header.timestamp);
                var length = header.length;
                var fragmentsStart = header.fragmentsStart;

                foreach (var p in ResendRequest.GetPacketNums(buffer, fragmentsStart))
                {
                    foreach (var bytes in data.GetFragments(p))
                        Socket.SendTo(bytes, data.Endpoint);
                }

                foreach (var p in ResendRequest.GetFragments(buffer, fragmentsStart, length))
                {
                    var bytes = data.GetFragment(p.packetNum, p.fragment);
                    Socket.SendTo(bytes, data.Endpoint);
                }

                return RudpResult.ResendRequest;
            }
            else
            {
                var header = new Packet.Header();
                header.FromBytes(buffer);

                data.UpdateLatency(header.timestamp);
                var packetNum = header.packetNum;
                var isPingRequest = header.pingRequest;
                var length = header.length;
                var fragmented = header.fragmented;
                var fragments = header.fragments;

                if (isPingRequest)
                    return RudpResult.PingRequest;

                data.AddIncomingPacket(buffer.AsSpan(0, length).ToArray(), packetNum, fragmented, fragments);

                return RudpResult.NewPacket;
            }
        }

        public void RequestMissingPackets()
        {
            for (int i = 0; i < _endpoints.Count; i++)
            {
                _endpoints[i].ClearExpiredMissingPackets();
                if (_endpoints[i].IsMissingPackets || _endpoints[i].IsMissingFragments)
                {
                    var bytes = _resendRequest.GetRequest(_endpoints[i].MissingPackets, _endpoints[i].MissingFragments);
                    Socket.SendTo(bytes.ToArray(), _endpoints[i].Endpoint);
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

            var packetNum = data.AddSentPacket(bytes);

            foreach (var fragment in data.GetFragments(packetNum))
                Socket.SendTo(fragment, endpoint);

            return bytes.Length;
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