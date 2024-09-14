
namespace OwlTree
{
    /// <summary>
    /// Base class for any object type that can be synchronously spawned.
    /// </summary>
    public class NetworkObject : IEncodable
    {
        // collect all sub-types
        internal static IEnumerable<Type> GetNetworkObjectTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract && typeof(NetworkObject).IsAssignableFrom(t));
        }

        /// <summary>
        /// Basic function signature for passing NetworkObjects.
        /// </summary>
        public delegate void Delegate(NetworkObject obj);

        /// <summary>
        /// The object's network id. This is synchronized across clients.
        /// </summary>
        public NetworkId Id { get; private set; }

        /// <summary>
        /// Whether or not the object is currently being managed across clients. If false, 
        /// then the object has been "destroyed".
        /// </summary>
        public bool IsActive { get; private set; }
        
        /// <summary>
        /// The connection this object associated with, and managed by.
        /// </summary>
        public Connection? Connection { get; private set; }

        /// <summary>
        /// FOR INTERNAL FRAMEWORK USE ONLY. Sets the object's network id.
        /// </summary>
        internal void SetIdInternal(NetworkId id)
        {
            Id = id;
        }

        /// <summary>
        /// FOR INTERNAL USE ONLY. Sets whether the object is active. If false, 
        /// then the object has been "destroyed".
        /// </summary>
        /// <param name="state"></param>
        internal void SetActiveInternal(bool state)
        {
            IsActive = state;
        }

        /// <summary>
        /// FOR INTERNAL USE ONLY. Sets the connection this object is associated with.
        /// </summary>
        /// <param name="connection"></param>
        internal void SetConnectionInternal(Connection connection)
        {
            Connection = connection;
        }

        /// <summary>
        /// Create a new NetworkObject, and assign it the given network id.
        /// </summary>
        public NetworkObject(NetworkId id)
        {
            Id = id;
        }

        /// <summary>
        /// Create a new NetworkObject. Id defaults to NetworkId.None.
        /// </summary>
        public NetworkObject()
        {
            Id = NetworkId.None;
        }

        /// <summary>
        /// Invoked when this object is spawned.
        /// </summary>
        public virtual void OnSpawn() { }

        /// <summary>
        /// Invoked when this object is destroyed.
        /// </summary>
        public virtual void OnDestroy() { }

        // [Rpc(RpcCaller.Server)]
        // public void TestRpc(NetworkId id, int i)
        // {
        //     var bytes = RpcAttribute.EncodeRPC(this, id, i);
        //     Console.WriteLine(BitConverter.ToString(bytes));
        //     var args = RpcAttribute.DecodeRPC(bytes);
        //     Console.WriteLine("Id: " + args[0]?.ToString());
        //     Console.WriteLine("i: " + args[1]?.ToString());
        // }

        // [Rpc(RpcCaller.Any)]
        // public void TestRpc2(ClientId id, float a, float b, string x)
        // {
        //     Console.WriteLine("Client Id: " + id.ToString());
        // }

        public byte[] ToBytes()
        {
            return Id.ToBytes();
        }

        public bool InsertBytes(ref byte[] bytes, ref int ind)
        {
            return Id.InsertBytes(ref bytes, ref ind);
        }

        public int ExpectedLength()
        {
            return Id.ExpectedLength();
        }

        public static object FromBytes(byte[] bytes)
        {
            return NetworkId.FromBytes(bytes);
        }

        public static object FromBytesAt(byte[] bytes, ref int ind)
        {
            return NetworkId.FromBytesAt(bytes, ref ind);
        }

        public static int MaxLength()
        {
            return NetworkId.MaxLength();
        }
    }
}