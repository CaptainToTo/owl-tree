

namespace OwlTree
{
    public class NetworkBuffer
    {
        public struct Message
        {
            public ClientId source;
            public ClientId target;
            public byte[]? bytes;

            public Message(ClientId source, ClientId target, byte[]? bytes)
            {
                this.source = source;
                this.target = target;
                this.bytes = bytes;
            }

            public static Message Empty = new Message(ClientId.None, ClientId.None, null);

            public bool IsEmpty { get { return bytes == null || bytes.Length == 0; } }
        }
    }
}