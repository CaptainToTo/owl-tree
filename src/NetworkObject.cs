
namespace OwlTree
{
    public class NetworkObject
    {
        public NetworkId Id { get; private set; }

        public NetworkObject(NetworkId id)
        {
            Id = id;
        }

        [Rpc(RpcCaller.Server)]
        private void TestRpc(NetworkId id, int i)
        {
            Console.WriteLine("Network Id: " + id.ToString());
        }

        [Rpc(RpcCaller.Any)]
        public void TestRpc2(ClientId id, float a, float b, string x)
        {
            Console.WriteLine("Client Id: " + id.ToString());
        }
    }
}