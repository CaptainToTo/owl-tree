using System;

namespace OwlTree
{
    public class Fragment
    {
        public struct Header
        {
            internal const int ByteLength = 18;

            // 1 byte for packet or fragment

            // 4 bytes
            /// <summary>
            /// The byte index offset this fragment starts at in the original packet.
            /// </summary>
            public int start { get; internal set; }

            // 4 bytes
            /// <summary>
            /// The byte length of the fragment, including its header.
            /// </summary>
            public int length { get; internal set; }

            public long timestamp { get; internal set; }

            // 4 bytes
            /// <summary>
            /// The unique UInt32 assigned to this client which is kept secret between the server and that client.
            /// </summary>
            public uint hash { get; internal set; }

            // 4 bytes
            /// <summary>
            /// The packet id this fragment is a part of.
            /// </summary>
            public uint packetNum { get; internal set; }

            // 1 byte
            /// <summary>
            /// The fragment number, use for ordering fragments for reconstruction.
            /// </summary>
            public byte fragment { get; internal set; }

            public void InsertBytes(Span<byte> bytes)
            {
                int ind = 0;
                bytes[ind] = PacketType.Fragment;
                ind += 1;

                Encoder.InsertBytes(bytes.Slice(ind), start);
                ind += 4;

                Encoder.InsertBytes(bytes.Slice(ind), length);
                ind += 4;

                Encoder.InsertBytes(bytes.Slice(ind), timestamp);
                ind += 8;

                Encoder.InsertBytes(bytes.Slice(ind), hash);
                ind += 4;

                Encoder.InsertBytes(bytes.Slice(ind), packetNum);
                ind += 4;

                bytes[ind] = fragment;
            }

            public void FromBytes(ReadOnlySpan<byte> bytes)
            {
                int ind = 0;

                if (!PacketType.IsFragment(bytes[ind]))
                    throw new ArgumentException("The provided bytes aren't a packet fragment.");
                ind += 1;

                start = Encoder.DecodeInt32(bytes.Slice(ind));
                ind += 4;

                length = Encoder.DecodeInt32(bytes.Slice(ind));
                ind += 4;

                if (length < ByteLength)
                    throw new ArgumentException("Fragment length is less than the minimum, this is not a complete fragment.");
                    
                timestamp = Encoder.DecodeInt64(bytes.Slice(ind));
                ind += 8;

                hash = Encoder.DecodeUInt32(bytes.Slice(ind));
                ind += 4;

                packetNum = Encoder.DecodeUInt32(bytes.Slice(ind));
                ind += 4;

                fragment = bytes[ind];
            }

            public void Reset()
            {
                start = 0;
                length = 0;
                hash = 0;
                fragment = 0;
            }

        }

        private byte[] _buffer;

        public Header header;

        public Fragment()
        {
            
        }

        public Fragment(Packet p, int fragment, int start, int length)
        {
            Reset(p, fragment, start, length);
        }

        public void Reset(Packet p, int fragment, int start, int length)
        {
            if (_buffer == null)
                _buffer = new byte[Header.ByteLength + length];
            else if (_buffer.Length < Header.ByteLength + length)
                Array.Resize(ref _buffer, Header.ByteLength + length);
            
            header.start = start;
            header.length = length + Header.ByteLength;
            header.hash = p.header.hash;
            header.fragment = (byte)fragment;

            var span = p.AsSpan(start, length);
            for (int i = 0; i < span.Length; i++)
                _buffer[Header.ByteLength + i] = span[i];
        }

        public Span<byte> GetFragment()
        {
            header.InsertBytes(_buffer);
            return _buffer.AsSpan(0, header.length);
        }
    }
}