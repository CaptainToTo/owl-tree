
namespace OwlTree
{
    public class NetworkObject
    {
        public NetworkId Id { get; private set; }
        public bool IsActive { get; private set; }

        internal void SetIdInternal(NetworkId id)
        {
            Id = id;
        }

        internal void SetActiveInternal(bool state)
        {
            IsActive = state;
        }

        public NetworkObject(NetworkId id)
        {
            Id = id;
        }

        public NetworkObject()
        {
            Id = NetworkId.None;
        }

        [Rpc(RpcCaller.Server)]
        public void TestRpc(NetworkId id, int i)
        {
            var bytes = RpcAttribute.EncodeRPC(this, id, i);
            Console.WriteLine(BitConverter.ToString(bytes));
            var args = RpcAttribute.DecodeRPC(bytes);
            Console.WriteLine("Id: " + args[0]?.ToString());
            Console.WriteLine("i: " + args[1]?.ToString());
        }

        [Rpc(RpcCaller.Any)]
        public void TestRpc2(ClientId id, float a, float b, string x)
        {
            Console.WriteLine("Client Id: " + id.ToString());
        }
    }
}