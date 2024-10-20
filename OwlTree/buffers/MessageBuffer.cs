
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
        public bool IsEmpty { get { return _tail == 0; } }

        /// <summary>
        /// Returns true if the buffer is full.
        /// </summary>
        public bool IsFull { get { return _tail == _buffer.Length; } }

        /// <summary>
        /// Create a new buffer with an initial size of bufferLen.
        /// </summary>
        public MessageBuffer(int bufferLen)
        {
            _buffer = new byte[bufferLen];
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
            if (byteCount > ushort.MaxValue)
                throw new ArgumentOutOfRangeException("message length is too long. Cannot be represented in a ushort (<65535).");

            ushort len = (ushort)byteCount;

            if (!HasSpaceFor(len + 2))
                Array.Resize(ref _buffer, _buffer.Length * 2);
            
            BitConverter.TryWriteBytes(_buffer.AsSpan(_tail), len);
            _tail += 2;

            for (int i = _tail; i < _tail + len; i++)
                _buffer[i] = 0;

            var span = _buffer.AsSpan(_tail, len);
            _tail += len;

            return span;
        }

        /// <summary>
        /// Empty the buffer.
        /// </summary>
        public void Reset() { _tail = 0; }

        /// <summary>
        /// Splits the given stream into individual message byte arrays.
        /// Uses the start argument to track where the next message should be read from. Returns false if the end of the stream
        /// has been reached, and there are no more messages to be read.
        /// </summary>
        public static bool GetNextMessage(ReadOnlySpan<byte> stream, ref int start, out ReadOnlySpan<byte> message)
        {
            message = new Span<byte>();
            if (start >= stream.Length)
                return false;
            
            var len = BitConverter.ToUInt16(stream.Slice(start));

            if (len == 0 || start + len > stream.Length)
                return false;
            
            message = stream.Slice(start + 2, len);
            start += 2 + len;
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