
using System;

namespace OwlTree
{
    /// <summary>
    /// Handles concatenating messages into a single buffer so that they can be sent in a single package.
    /// messages are stacked in the format: <br />
    /// <c>[message byte length][message bytes][message byte length][message bytes]...</c>
    /// </summary>
    public class MessageBuffer
    {
        private byte[] _buffer; // the actual byte buffer containing
        private int _tail = 0;  // the current end of the buffer
        private UInt32 _hash = 0; // if the buffer is for a udp socket, prepend this hash
        
        /// <summary>
        /// Gets a span of all added messages. This will exclude empty bytes at the end of the buffer.
        /// </summary>
        public Span<byte> GetBuffer()
        {
            return _buffer.AsSpan(0, _tail);
        }
        
        /// <summary>
        /// Returns true if the buffer is empty.
        /// </summary>
        public bool IsEmpty { get { return IsUdp ? _tail == 4 : _tail == 0; } }

        /// <summary>
        /// Returns true if the buffer is full.
        /// </summary>
        public bool IsFull { get { return _tail == _buffer.Length; } }

        public bool IsUdp { get; private set; } = false;

        /// <summary>
        /// Create a new buffer with an initial size of bufferLen.
        /// </summary>
        public MessageBuffer(int bufferLen)
        {
            _buffer = new byte[bufferLen];
        }

        public MessageBuffer(int bufferLen, UInt32 hash)
        {
            _buffer = new byte[bufferLen];
            IsUdp = true;
            _hash = hash;
            BitConverter.TryWriteBytes(_buffer, _hash);
            _tail = 4;
        }

        /// <summary>
        /// Returns true if the buffer has space to add the specified number of bytes without needing to resize.
        /// </summary>
        public bool HasSpaceFor(int bytes)
        {
            return _tail + bytes < _buffer.Length;
        }

        /// <summary>
        /// Gets space for a new message, which can be written into using to provided span. 
        /// If there isn't enough space for the given number of bytes, the buffer will double in size.
        /// Messages are stacked in the format: <br />
        /// <c>[message byte length][message bytes][message byte length][message bytes]...</c> 
        /// </summary>
        public Span<byte> GetSpan(int byteCount)
        {
            if (!HasSpaceFor(byteCount + 4))
                Array.Resize(ref _buffer, _buffer.Length * 2);
            
            BitConverter.TryWriteBytes(_buffer.AsSpan(_tail), byteCount);
            _tail += 4;

            for (int i = _tail; i < _tail + byteCount; i++)
                _buffer[i] = 0;

            var span = _buffer.AsSpan(_tail, byteCount);
            _tail += byteCount;

            return span;
        }

        /// <summary>
        /// Empty the buffer.
        /// </summary>
        public void Reset() { _tail = IsUdp ? 4 : 0; }

        /// <summary>
        /// Splits the given stream into individual message byte arrays.
        /// Uses the start argument to track where the next message should be read from. Returns false if the end of the stream
        /// has been reached, and there are no more messages to be read.
        /// </summary>
        public static bool GetNextMessage(Span<byte> stream, ref int start, out ReadOnlySpan<byte> message)
        {
            message = new Span<byte>();
            if (start >= stream.Length)
                return false;
            
            var len = BitConverter.ToInt32(stream.Slice(start));

            if (len == 0 || start + len > stream.Length)
                return false;
            
            message = stream.Slice(start + 4, len);
            start += 4 + len;
            return true;
        }

        /// <summary>
        /// Get the current buffer as a string of hex values.
        /// </summary>
        public override string ToString()
        {
            return BitConverter.ToString(_buffer);
        }
    }
}