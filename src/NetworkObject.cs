
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
        private void TestRpc(NetworkId id)
        {
            Console.WriteLine("Network Id: " + id.ToString());
        }
    }
}