
namespace OwlTree
{
    public class NetworkObject
    {
        public NetworkId Id { get; private set; }

        public NetworkObject(NetworkId id)
        {
            Id = id;
        }
    }
}