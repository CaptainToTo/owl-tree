
namespace OwlTree
{
    public class SendBuffer
    {
        private byte[] _buffer;
        private int _tail = 0;

        public byte[] Buffer { get { return _buffer; } }

        public bool IsEmpty { get { return _tail == 0; } }
        public bool IsFull { get { return _tail == _buffer.Length; } }

        public SendBuffer(int bufferLen)
        {
            _buffer = new byte[bufferLen];
        }

        public bool HasSpaceFor(int bytes)
        {
            return _tail + bytes < _buffer.Length;
        }

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

        public void Reset()
        {
            _tail = 0;
        }

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