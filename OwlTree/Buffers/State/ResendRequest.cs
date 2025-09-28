using System;
using System.Collections.Generic;
using System.Linq;

namespace OwlTree
{
    public class ResendRequest
    {
        public struct Header
        {
            internal const int ByteLength = 24;

            public int length { get; internal set; }

            public long timestamp { get; internal set; }

            public uint hash { get; internal set; }

            public int fragmentsStart { get; internal set; }

            public void InsertBytes(Span<byte> bytes)
            {
                int ind = 0;
                bytes[ind] = PacketType.ResendRequest;
                ind += 1;

                Encoder.InsertBytes(bytes.Slice(ind), length);
                ind += 4;

                Encoder.InsertBytes(bytes.Slice(ind), timestamp);
                ind += 8;

                Encoder.InsertBytes(bytes.Slice(ind), hash);
                ind += 4;

                Encoder.InsertBytes(bytes.Slice(ind), fragmentsStart);
            }

            public void FromBytes(ReadOnlySpan<byte> bytes)
            {
                int ind = 0;

                if (!PacketType.IsResendRequest(bytes[ind]))
                    throw new ArgumentException("The provided bytes aren't a resend request.");
                ind += 1;

                length = Encoder.DecodeInt32(bytes.Slice(ind));
                ind += 4;

                if (length < ByteLength)
                    throw new ArgumentException("Data length is less than the minimum, this is not a complete resend request.");

                timestamp = Encoder.DecodeInt64(bytes.Slice(ind));
                ind += 8;

                hash = Encoder.DecodeUInt32(bytes.Slice(ind));
                ind += 4;

                fragmentsStart = Encoder.DecodeInt32(bytes.Slice(ind));
            }

            public void Reset()
            {
                length = 0;
                timestamp = 0;
                hash = 0;
                fragmentsStart = 0;
            }
        }

        private byte[] _buffer;

        public Header header;

        public ResendRequest()
        {
            _buffer = new byte[Header.ByteLength];
        }

        public Span<byte> GetRequest(IEnumerable<uint> missingPackets, IEnumerable<(uint packetNum, byte fragment)> missingFragments)
        {
            var dataLen = (missingPackets.Count() * 4) + (missingFragments.Count() * 5) + Header.ByteLength;

            if (_buffer.Length < dataLen)
                Array.Resize(ref _buffer, dataLen);

            var ind = Header.ByteLength;
            foreach (var p in missingPackets)
            {
                Encoder.InsertBytes(_buffer.AsSpan(ind), p);
                ind += 4;
            }

            header.fragmentsStart = ind;
            foreach (var p in missingFragments)
            {
                Encoder.InsertBytes(_buffer.AsSpan(ind), p.packetNum);
                _buffer[ind + 4] = p.fragment;
                ind += 5;
            }

            header.timestamp = Timestamp.Now;
            header.length = dataLen;
            header.InsertBytes(_buffer);

            return _buffer.AsSpan(0, header.length);
        }

        public static IEnumerable<uint> GetPacketNums(byte[] bytes, int fragmentsStart)
        {
            int ind = Header.ByteLength;
            while (ind < fragmentsStart)
            {
                yield return Encoder.DecodeUInt32(bytes.AsSpan(ind));
                ind += 4;
            }
        }

        public static IEnumerable<(uint packetNum, byte fragment)> GetFragments(byte[] bytes, int fragmentsStart, int fullLength)
        {
            int ind = fragmentsStart;
            while (ind < fullLength)
            {
                yield return (Encoder.DecodeUInt32(bytes.AsSpan(ind)), bytes[ind + 4]);
                ind += 5;
            }
        }
    }
}