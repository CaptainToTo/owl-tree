
namespace OwlTree
{
    /// <summary>
    /// Handles concatenating messages into a single buffer so that they can be sent in a single package.
    /// messages are stacked in the format: <br />
    /// <c>[RPC byte length][RPC bytes][RPC byte length][RPC bytes]...</c>
    /// </summary>
    public class MessageBuffer
    {
        private byte[] _buffer; // the actual byte buffer containing
        private int _tail = 0;  // the current end of the buffer
        
        /// <summary>
        /// Produces a copy of the byte array buffer excluding trailing 0-bytes.
        /// </summary>
        public ReadOnlySpan<byte> GetBuffer()
        {
            return _buffer.AsSpan(0, _tail);
        }
        
        /// <summary>
        /// Returns true if the buffer is empty.
        /// </summary>
        public bool IsEmpty { get { return _tail == 0; } }

        /// <summary>
        /// Returns true if the buffer is full, and cannot have anymore RPCs added to it.
        /// </summary>
        public bool IsFull { get { return _tail == _buffer.Length; } }

        /// <summary>
        /// Create a new buffer with a max size of bufferLen.
        /// </summary>
        public MessageBuffer(int bufferLen)
        {
            _buffer = new byte[bufferLen];
        }

        /// <summary>
        /// Returns true if the buffer has space to add the specified number of bytes.
        /// </summary>
        public bool HasSpaceFor(int bytes)
        {
            return _tail + bytes < _buffer.Length;
        }

        /// <summary>
        /// Gets space for a new message, which can be written into using to provided span. 
        /// This will fail if there isn't enough space in the buffer.
        /// Messages are stacked in the format: <br />
        /// <c>[message byte length][message bytes][message byte length][message bytes]...</c>
        /// </summary>
        public Span<byte> GetSpan(int byteCount)
        {
            if (byteCount > ushort.MaxValue)
                throw new ArgumentOutOfRangeException("message length is too long. Cannot be represented in a byte (<255).");

            ushort len = (ushort)byteCount;

            if (!HasSpaceFor(len + 2))
                throw new ArgumentOutOfRangeException("Buffer is too full to add " + len + " bytes.");
            
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
        /// Splits the given stream into individual message byte arrays. These byte arrays are added to the messages list.
        /// </summary>
        public static void SplitMessageBytes(byte[] stream, ref List<byte[]> messages)
        {
            bool reading = false;
            int curIndex = 0;
            byte[] curBytes = {};
            for (int i = 0; i < stream.Length; i++)
            {
                if (!reading)
                {
                    int len = BitConverter.ToUInt16(stream.AsSpan(i));
                    if (len == 0)
                        break;
                    curBytes = new byte[len];
                    curIndex = 0;
                    reading = true;
                    i++;
                    continue;
                }

                curBytes[curIndex] = stream[i];
                curIndex++;
                if (curIndex >= curBytes.Length)
                {
                    messages.Add(curBytes);
                    reading = false;
                }
            }
            if (reading)
            {
                messages.Add(curBytes);
            }
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