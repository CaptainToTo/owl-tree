
namespace OwlTree
{
    /// <summary>
    /// Handles concatenating RPCs into a single buffer so that they can be sent in a single package.
    /// RPCs are stacked in the format: <br />
    /// <c>[RPC byte length][RPC bytes][RPC byte length][RPC bytes]...</c>
    /// </summary>
    public class SendBuffer
    {
        private byte[] _buffer; // the actual byte buffer containing
        private int _tail = 0;  // the current end of the buffer
        
        /// <summary>
        /// Produces a copy of the byte array buffer excluding trailing 0-bytes.
        /// </summary>
        public byte[] GetBuffer()
        {
            byte[] copy = new byte[_tail];
            _buffer.CopyTo(copy, 0);
            return copy;
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
        public SendBuffer(int bufferLen)
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
        /// Concatenates the given byte array to the buffer. This will fail if there isn't enough space in the buffer.
        /// RPCs are stacked in the format: <br />
        /// <c>[RPC byte length][RPC bytes][RPC byte length][RPC bytes]...</c>
        /// </summary>
        public void Add(byte[] rpcBytes)
        {
            if (rpcBytes.Length > 255)
                throw new ArgumentOutOfRangeException("RPC length is too long. Cannot be represented in a byte (<255).");

            byte len = (byte)rpcBytes.Length;

            if (!HasSpaceFor(len + 1))
                throw new ArgumentOutOfRangeException("Buffer is too full to add " + len + " bytes.");
            
            _buffer[_tail] = len;
            _tail++;
            var end = _tail + len;

            for (int i = _tail; i < end; i++)
            {
                _buffer[_tail] = rpcBytes[i];
                _tail++;
            }
        }

        /// <summary>
        /// Empty the buffer.
        /// </summary>
        public void Reset() { _tail = 0; }

        /// <summary>
        /// Splits the given stream into individual RPC encoded byte arrays. These byte arrays are added to the rpcBytes list.
        /// </summary>
        public static void GetRpcBytes(byte[] stream, ref List<byte[]> rpcBytes)
        {
            bool reading = false;
            int curRpcIndex = 0;
            byte[] curRpcBytes = {};
            for (int i = 0; i < stream.Length; i++)
            {
                if (!reading)
                {
                    curRpcBytes = new byte[stream[i]];
                    curRpcIndex = 0;
                    reading = true;
                    continue;
                }

                curRpcBytes[curRpcIndex] = stream[i];
                curRpcIndex++;
                if (curRpcIndex >= curRpcBytes.Length)
                {
                    rpcBytes.Add(curRpcBytes);
                    reading = false;
                }
            }
        }
    }
}