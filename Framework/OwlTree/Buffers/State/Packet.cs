
using System;
using System.Linq;

namespace OwlTree
{
    /// <summary>
    /// Handles concatenating messages into a single buffer so that they can be sent in a single packet.
    /// messages are stacked in the format: <br />
    /// <c>[packet header][message byte length][message bytes][message byte length][message bytes]...</c>
    /// </summary>
    public class Packet
    {
        public struct Header
        {
            internal const int BYTE_LEN = 28;

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

            // 1 byte
            /// <summary>
            /// Reserved flag for signifying whether or not compression was used on this packet.
            /// </summary>
            public bool compressionEnabled { get; internal set; }
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
            /// <summary>
            /// Available header flag for application specific use.
            /// </summary>
            public bool flag5;
            /// <summary>
            /// Available header flag for application specific use.
            /// </summary>
            public bool flag6;
            /// <summary>
            /// Available header flag for application specific use.
            /// </summary>
            public bool flag7;

            // 8 bytes
            /// <summary>
            /// The Unix Epoch millisecond timestamp this packet was sent at.
            /// </summary>
            public long timestamp { get; internal set; }

            // 4 bytes
            /// <summary>
            /// The number of bytes in the packet, including the header. To get the number of bytes, excluding the header,
            /// subtract <c>Header.BYTE_LEN</c> from this.
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

            public void InsertBytes(Span<byte> bytes)
            {
                int ind = 0;
                BitConverter.TryWriteBytes(bytes, owlTreeVer);
                ind += 2;

                BitConverter.TryWriteBytes(bytes.Slice(ind), appVer);
                ind += 2;

                BitConverter.TryWriteBytes(bytes.Slice(ind), timestamp);
                ind += 8;

                BitConverter.TryWriteBytes(bytes.Slice(ind), length);
                ind += 4;

                BitConverter.TryWriteBytes(bytes.Slice(ind), sender);
                ind += 4;

                BitConverter.TryWriteBytes(bytes.Slice(ind), hash);
                ind += 4;

                byte flags = 0;
                flags |= (byte)(compressionEnabled ? 0x1 : 0);
                flags |= (byte)(flag1 ? 0x1 << 1 : 0);
                flags |= (byte)(flag2 ? 0x1 << 2 : 0);
                flags |= (byte)(flag3 ? 0x1 << 3 : 0);
                flags |= (byte)(flag4 ? 0x1 << 4 : 0);
                flags |= (byte)(flag5 ? 0x1 << 5 : 0);
                flags |= (byte)(flag6 ? 0x1 << 6 : 0);
                flags |= (byte)(flag7 ? 0x1 << 7 : 0);
                bytes[ind] = flags;
            }

            public void FromBytes(ReadOnlySpan<byte> bytes)
            {
                int ind = 0;
                owlTreeVer = BitConverter.ToUInt16(bytes);
                ind += 2;

                appVer = BitConverter.ToUInt16(bytes.Slice(ind));
                ind += 2;

                timestamp = BitConverter.ToInt64(bytes.Slice(ind));
                ind += 8;

                length = BitConverter.ToInt32(bytes.Slice(ind));
                ind += 4;

                if (length <= BYTE_LEN)
                    throw new ArgumentException("Packet length is less than the minimum, this is not a complete packet.");

                sender = BitConverter.ToUInt32(bytes.Slice(ind));
                ind += 4;

                hash = BitConverter.ToUInt32(bytes.Slice(ind));
                ind += 4;

                byte flags = bytes[ind];
                compressionEnabled = (flags & 0x1) == 1;
                flag1 = (flags & (0x1 << 1)) != 0;
                flag2 = (flags & (0x1 << 2)) != 0;
                flag3 = (flags & (0x1 << 3)) != 0;
                flag4 = (flags & (0x1 << 4)) != 0;
                flag5 = (flags & (0x1 << 5)) != 0;
                flag6 = (flags & (0x1 << 6)) != 0;
                flag7 = (flags & (0x1 << 7)) != 0;
            }

            public void Reset()
            {
                timestamp = 0;
                length = 0;
                sender = 0;
                hash = 0;
                compressionEnabled = false;
                flag1 = false;
                flag2 = false;
                flag3 = false;
                flag4 = false;
                flag5 = false;
                flag6 = false;
                flag7 = false;
            }
        }

        /// <summary>
        /// The number bytes currently used in the packet.
        /// </summary>
        public int Length { get { return _tail; } }

        private int _fragmentSize;
        private int _endOfFragment = 0;
        private int _startOfNextFragment = 0;
        private bool _useFragments;

        private bool FragmentationNeeded { get { return _tail > _fragmentSize; } }

        private byte[] _buffer; // the actual byte buffer containing
        private int _tail = 0;  // the current end of the buffer
        /// <summary>
        /// Struct containing data that will be contained in the header of the packet.
        /// </summary>
        public Header header;

        /// <summary>
        /// Create a new packet buffer with an initial size of bufferLen.
        /// </summary>
        public Packet(int bufferLen, bool useFragments = false)
        {
            _useFragments = useFragments;
            _fragmentSize = bufferLen;
            _buffer = new byte[bufferLen];
            _tail = Header.BYTE_LEN;
        }

        /// <summary>
        /// Returns true if the packet is empty.
        /// </summary>
        public bool IsEmpty { get { return _tail == Header.BYTE_LEN; } }

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
            if (_useFragments && FragmentationNeeded)
            {
                _endOfFragment = Header.BYTE_LEN + size;
            }
            else
            {
                _tail = Header.BYTE_LEN + size;
            }
        }
        
        /// <summary>
        /// Gets a span of the full packet. This will exclude empty bytes at the end of the buffer.
        /// </summary>
        internal Span<byte> GetPacket()
        {
            header.length = (_useFragments && FragmentationNeeded) ? _endOfFragment : _tail;
            header.InsertBytes(_buffer);
            return _buffer.AsSpan(0, (_useFragments && FragmentationNeeded) ? _endOfFragment : _tail);
        }

        /// <summary>
        /// Gets a span of the full buffer excluding the header.
        /// </summary>
        public Span<byte> GetBuffer()
        {
            return _buffer.AsSpan(Header.BYTE_LEN);
        }

        /// <summary>
        /// Gets a span of the packet bytes excluding the header.
        /// </summary>
        public Span<byte> GetMessages()
        {
            return _buffer.AsSpan(Header.BYTE_LEN, ((_useFragments && FragmentationNeeded) ? _endOfFragment : _tail) - Header.BYTE_LEN);
        }

        /// <summary>
        /// Gets space for a new message, which can be written into using to provided span. 
        /// If there isn't enough space for the given number of bytes, the buffer will double in size.
        /// </summary>
        public Span<byte> GetSpan(int byteCount)
        {
            if (!HasSpaceFor(byteCount + 4))
                Array.Resize(ref _buffer, _buffer.Length * 2);

            if (byteCount + 4 + _tail > _fragmentSize && _endOfFragment == 0)
            {
                _endOfFragment = _tail;
                _startOfNextFragment = _tail;
            }
            
            BitConverter.TryWriteBytes(_buffer.AsSpan(_tail), byteCount);
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

        internal int FromBytes(byte[] bytes, int start)
        {
            int i = start;
            if (!Incomplete)
            {
                header.FromBytes(bytes.AsSpan(i));
                Incomplete = bytes.Length < header.length; 

                if (header.length > _buffer.Length)
                    Array.Resize(ref _buffer, header.length + 1);
                
                _tail = Header.BYTE_LEN;
                i = start + Header.BYTE_LEN;
            }

            for (; (i < bytes.Length) && (_tail < header.length); i++)
            {
                _buffer[_tail] = bytes[i];
                _tail++;
            }

            if (_tail == header.length)
            {
                Incomplete = false;
            }
            return i;
        }

        /// <summary>
        /// Empty the buffer of bytes that currently would be sent using <c>GetPacket()</c>.
        /// </summary>
        internal void Reset() 
        { 
            header.Reset();
            // if no fragmentation used, just reset indices
            if (!_useFragments || !FragmentationNeeded)
            {
                _tail = Header.BYTE_LEN;
                _endOfFragment = 0;
                _startOfNextFragment = 0;
            }
            // if fragmentation was used, shift bytes over for next fragment, and find the new end of fragment index
            else if (FragmentationNeeded)
            {
                int nextFragmentLen = Header.BYTE_LEN;
                int remainingBytes = _tail - _startOfNextFragment;
                int lastByte = _startOfNextFragment;
                _endOfFragment = 0;
                _startOfNextFragment = 0;
                for (int i = 0; i < remainingBytes;)
                {
                    var len = BitConverter.ToInt32(_buffer.AsSpan(lastByte + i));
                    if (nextFragmentLen + len + 4 > _fragmentSize && _endOfFragment == 0)
                    {
                        _endOfFragment = nextFragmentLen;
                        _startOfNextFragment = nextFragmentLen;
                    }
                    else
                        nextFragmentLen += len + 4;

                    BitConverter.TryWriteBytes(_buffer.AsSpan(Header.BYTE_LEN + i), len);
                    i += 4;
                    for (int j = 0; j < len; j++)
                    {
                        var b = _buffer[lastByte + i + j];
                        _buffer[Header.BYTE_LEN + i + j] = b;
                    }
                    i += len;
                }
                _tail = Header.BYTE_LEN + remainingBytes;
            }
        }

        internal void Clear()
        {
            for (int i = 0; i < _buffer.Length; i++)
            {
                _buffer[i] = 0;
            }
            header.Reset();
            _tail = Header.BYTE_LEN;
            _endOfFragment = 0;
            _startOfNextFragment = 0;
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
            
            var len = BitConverter.ToInt32(bytes.Slice(_start));

            if (len == 0 || _start + len > bytes.Length)
                return false;
            
            message = bytes.Slice(_start + 4, len);
            _start += 4 + len;
            return true;
        }

        /// <summary>
        /// Get the current buffer as a string of hex values.
        /// </summary>
        public override string ToString()
        {
            return BitConverter.ToString(GetPacket().ToArray());
        }
    }
}