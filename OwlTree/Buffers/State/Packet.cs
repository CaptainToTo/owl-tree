
using System;
using System.Collections;
using System.Linq;
using System.Text;

namespace OwlTree
{
    internal static class PacketType
    {
        public const byte PacketStart = 0;
        public const byte Fragment = 1;
        public const byte ResendRequest = 2;

        public static bool IsPacketStart(byte b) => b == PacketStart;
        public static bool IsFragment(byte b) => b == Fragment;
        public static bool IsResendRequest(byte b) => b == ResendRequest;
    }

    /// <summary>
    /// Handles concatenating messages into a single buffer so that they can be sent in a single packet.
    /// messages are stacked in the format: <br />
    /// <c>[packet header][message byte length][message bytes][message byte length][message bytes]...</c>
    /// </summary>
    public class Packet
    {
        internal const int MaxTransmissionUnit = 1300;

        public struct Header
        {
            internal const int ByteLength = 36;

            // 1 byte for packet or fragment

            // 2 bytes
            /// <summary>
            /// The specific version of OwlTree this packet was sent from.
            /// </summary>
            public ushort owlTreeVer { get; internal set; }

            // 2 bytes
            /// <summary>
            /// The specific version of your application this packet was sent from.
            /// </summary>
            public ushort appVer { get; internal set; }

            // 8 bytes
            /// <summary>
            /// The Unix Epoch millisecond timestamp this packet was sent at.
            /// </summary>
            public long timestamp { get; internal set; }

            // 4 bytes
            /// <summary>
            /// The number of bytes in the packet, including the header. To get the number of bytes, excluding the header,
            /// subtract <c>Header.ByteLength</c> from this.
            /// </summary>
            public int length { get; internal set; }

            // 4 bytes
            /// <summary>
            /// The client id of the client who sent this packet, as a UInt32.
            /// </summary>
            public uint sender { get; internal set; }

            // 4 bytes
            /// <summary>
            /// The unique UInt32 assigned to this client which is kept secret between the server and that client.
            /// </summary>
            public uint hash { get; internal set; }

            // 4 bytes
            /// <summary>
            /// The last packet received. Use to acknowledge a packet as received.
            /// </summary>
            public uint acknowledged { get; internal set; }

            // 4 bytes
            /// <summary>
            /// The ordered id of the packet. Used for reliable UDP packet transfer.
            /// </summary>
            public uint packetNum { get; internal set; }

            // 1 byte
            /// <summary>
            /// The number of fragments this packet has been broken into.
            /// </summary>
            public byte fragments { get; internal set; }

            // 1 byte
            /// <summary>
            /// Reserved flag for signifying whether or not compression was used on this packet.
            /// </summary>
            public bool compressionEnabled { get; internal set; }
            /// <summary>
            /// Reserved flag for signifying a specific packet is for sending ping requests.
            /// </summary>
            public bool pingRequest { get; internal set; }
            /// <summary>
            /// Reserved flag for signifying if a packet has been fragmented.
            /// </summary>
            public bool fragmented { get; internal set; }
            /// <summary>
            /// Available header flag for application specific use.
            /// </summary>
            public bool flag1;
            /// <summary>
            /// Available header flag for application specific use.
            /// </summary>
            public bool flag2;
            /// <summary>
            /// Available header flag for application specific use.
            /// </summary>
            public bool flag3;
            /// <summary>
            /// Available header flag for application specific use.
            /// </summary>
            public bool flag4;

            public void InsertBytes(Span<byte> bytes)
            {
                int ind = 0;
                bytes[ind] = PacketType.PacketStart;
                ind += 1;

                Encoder.InsertBytes(bytes, owlTreeVer);
                ind += 2;

                Encoder.InsertBytes(bytes.Slice(ind), appVer);
                ind += 2;

                Encoder.InsertBytes(bytes.Slice(ind), timestamp);
                ind += 8;

                Encoder.InsertBytes(bytes.Slice(ind), length);
                ind += 4;

                Encoder.InsertBytes(bytes.Slice(ind), sender);
                ind += 4;

                Encoder.InsertBytes(bytes.Slice(ind), hash);
                ind += 4;

                Encoder.InsertBytes(bytes.Slice(ind), acknowledged);
                ind += 4;

                Encoder.InsertBytes(bytes.Slice(ind), packetNum);
                ind += 4;

                bytes[ind] = fragments;

                byte flags = 0;
                flags |= (byte)(compressionEnabled ? 0x1 : 0);
                flags |= (byte)(pingRequest ? 0x1 << 1 : 0);
                flags |= (byte)(fragmented ? 0x1 << 2 : 0);
                flags |= (byte)(flag1 ? 0x1 << 3 : 0);
                flags |= (byte)(flag2 ? 0x1 << 4 : 0);
                flags |= (byte)(flag3 ? 0x1 << 5 : 0);
                flags |= (byte)(flag4 ? 0x1 << 6 : 0);
                bytes[ind] = flags;
            }

            public void FromBytes(ReadOnlySpan<byte> bytes)
            {
                int ind = 0;

                if (!PacketType.IsPacketStart(bytes[ind]))
                    throw new ArgumentException("The provided bytes aren't the start of a new packet.");
                ind += 1;

                owlTreeVer = Encoder.DecodeUInt16(bytes);
                ind += 2;

                appVer = Encoder.DecodeUInt16(bytes.Slice(ind));
                ind += 2;

                timestamp = Encoder.DecodeInt64(bytes.Slice(ind));
                ind += 8;

                length = Encoder.DecodeInt32(bytes.Slice(ind));
                ind += 4;

                if (length < ByteLength)
                    throw new ArgumentException("Packet length is less than the minimum, this is not a complete packet.");

                sender = Encoder.DecodeUInt32(bytes.Slice(ind));
                ind += 4;

                hash = Encoder.DecodeUInt32(bytes.Slice(ind));
                ind += 4;

                acknowledged = Encoder.DecodeUInt32(bytes.Slice(ind));
                ind += 4;

                packetNum = Encoder.DecodeUInt32(bytes.Slice(ind));
                ind += 4;

                fragments = bytes[ind];
                ind += 1;

                byte flags = bytes[ind];
                compressionEnabled = (flags & 0x1) == 1;
                pingRequest = (flags & (0x1 << 1)) != 0;
                fragmented = (flags & (0x1 << 2)) != 0;
                flag1 = (flags & (0x1 << 3)) != 0;
                flag2 = (flags & (0x1 << 4)) != 0;
                flag3 = (flags & (0x1 << 5)) != 0;
                flag4 = (flags & (0x1 << 6)) != 0;
            }

            public void Reset()
            {
                timestamp = 0;
                length = 0;
                sender = 0;
                hash = 0;
                acknowledged = 0;
                packetNum = 0;
                fragments = 0;
                compressionEnabled = false;
                pingRequest = false;
                fragmented = false;
                flag1 = false;
                flag2 = false;
                flag3 = false;
                flag4 = false;
            }
        }

        /// <summary>
        /// The number bytes currently used in the packet.
        /// </summary>
        public int Length => _tail;

        private byte[] _buffer; // the actual byte buffer containing
        private int _tail = 0;  // the current end of the buffer
        /// <summary>
        /// Struct containing data that will be contained in the header of the packet.
        /// </summary>
        public Header header;

        /// <summary>
        /// Create a new packet buffer with an initial size of bufferLen.
        /// </summary>
        public Packet(int bufferLen)
        {
            _buffer = new byte[bufferLen];
            _tail = Header.ByteLength;
        }

        /// <summary>
        /// Returns true if the packet is empty.
        /// </summary>
        public bool IsEmpty => _tail == Header.ByteLength;

        /// <summary>
        /// Returns true if the buffer has space to add the specified number of bytes without needing to resize.
        /// </summary>
        public bool HasSpaceFor(int bytes)
        {
            return _tail + bytes < _buffer.Length;
        }

        /// <summary>
        /// If the packet data has been resized from outside the class (such as for compression),
        /// update the byte length of the message portion of this packet. The given size excludes the 
        /// header size.
        /// </summary>
        public void SetSize(int size)
        {
            _tail = Header.ByteLength + size;
        }

        /// <summary>
        /// Gets a span of the full packet. This will exclude empty bytes at the end of the buffer.
        /// </summary>
        internal Span<byte> GetPacket()
        {
            header.length = _tail;
            header.InsertBytes(_buffer);
            return _buffer.AsSpan(0, _tail);
        }

        /// <summary>
        /// Gets a span of the full buffer excluding the header.
        /// </summary>
        public Span<byte> GetBuffer()
        {
            return _buffer.AsSpan(Header.ByteLength);
        }

        /// <summary>
        /// Gets a span of the packet bytes excluding the header.
        /// </summary>
        public Span<byte> GetMessages()
        {
            return _buffer.AsSpan(Header.ByteLength, _tail - Header.ByteLength);
        }

        public Span<byte> AsSpan(int start, int length)
        {
            return _buffer.AsSpan(start, length);
        }

        /// <summary>
        /// Gets space for a new message, which can be written into using to provided span. 
        /// If there isn't enough space for the given number of bytes, the buffer will double in size.
        /// </summary>
        public Span<byte> GetSpan(int byteCount)
        {
            if (!HasSpaceFor(byteCount + 4))
                Array.Resize(ref _buffer, _buffer.Length * 2);

            Encoder.InsertBytes(_buffer.AsSpan(_tail), byteCount);
            _tail += 4;

            for (int i = _tail; i < _tail + byteCount; i++)
                _buffer[i] = 0;

            var span = _buffer.AsSpan(_tail, byteCount);
            _tail += byteCount;

            return span;
        }

        /// <summary>
        /// True if there is missing data from this packet.
        /// </summary>
        public bool Incomplete { get; private set; } = false;

        internal int FromBytes(byte[] bytes, int start, int dataLen)
        {
            int i = start;
            if (!Incomplete)
            {
                if (dataLen - start >= Header.ByteLength)
                {
                    header.FromBytes(bytes.AsSpan(i));
                    Incomplete = dataLen - start < header.length;

                    if (header.length > _buffer.Length)
                        Array.Resize(ref _buffer, header.length + 1);

                    _tail = Header.ByteLength;
                    i = start + Header.ByteLength;
                }
                else
                {
                    header.length = 0;
                    for (_tail = 0; i < dataLen; i++)
                    {
                        _buffer[_tail] = bytes[i];
                        _tail++;
                    }
                    Incomplete = true;
                    return i - start;
                }
            }
            else if (_tail < Header.ByteLength)
            {
                for (; _tail < Header.ByteLength; _tail++)
                {
                    _buffer[_tail] = bytes[i];
                    i++;
                }
                header.FromBytes(_buffer);
                Incomplete = dataLen - i < header.length - Header.ByteLength;

                if (header.length > _buffer.Length)
                    Array.Resize(ref _buffer, header.length + 1);

                _tail = Header.ByteLength;
            }

            for (; (i < bytes.Length) && (_tail < header.length); i++)
            {
                _buffer[_tail] = bytes[i];
                _tail++;
            }

            if (_tail >= header.length)
            {
                Incomplete = false;
            }
            return i - start;
        }

        /// <summary>
        /// Resize the packet buffer to a new length. This length must be greater that what it currently is.
        /// </summary>
        internal void GrowTo(int length)
        {
            if (length + Header.ByteLength <= _buffer.Length)
                return;
            Array.Resize(ref _buffer, length + Header.ByteLength + 1);
        }

        /// <summary>
        /// Empty the buffer of bytes that currently would be sent using <c>GetPacket()</c>.
        /// </summary>
        internal void Reset()
        {
            header.Reset();
            _tail = Header.ByteLength;
        }

        internal void Clear()
        {
            for (int i = 0; i < _buffer.Length; i++)
            {
                _buffer[i] = 0;
            }
            header.Reset();
            _tail = Header.ByteLength;
            _start = 0;
        }

        private int _start = 0;

        internal void StartMessageRead()
        {
            _start = 0;
        }

        /// <summary>
        /// Splits this packet into the messages that compose it. Retrieves each next message, 
        /// until the entire packet has been split.
        /// </summary>
        internal bool TryGetNextMessage(out ReadOnlySpan<byte> message)
        {
            var bytes = GetMessages();
            message = new Span<byte>();
            if (_start >= bytes.Length - 4)
                return false;

            var len = Encoder.DecodeInt32(bytes.Slice(_start));

            if (len == 0 || _start + len + 4 > bytes.Length)
                return false;

            message = bytes.Slice(_start + 4, len);
            _start += 4 + len;
            return true;
        }
        
        public void ToString(StringBuilder str)
        {
            var packet = GetPacket();
            Encoder.ToString(packet, str, 32);
        }

        public override string ToString()
        {
            var packet = GetPacket();
            return Encoder.ToString(packet, 32);
        }
    }
}