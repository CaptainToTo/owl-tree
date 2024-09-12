namespace OwlTree
{
    public class NetworkSpawner
    {
        private static Dictionary<Type, byte> _typeToIds = new Dictionary<Type, byte>();
        private static Dictionary<byte, Type> _idsToType = new Dictionary<byte, Type>();

        private static IEnumerable<Type> GetNetworkObjectTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract && typeof(NetworkObject).IsAssignableFrom(t));
        }

        public NetworkSpawner(NetworkBuffer buffer)
        {
            byte id = 1;
            _typeToIds.Add(typeof(NetworkObject), id);
            _idsToType.Add(id, typeof(NetworkObject));

            id += 1;

            var subClasses = GetNetworkObjectTypes();

            foreach (var subClass in subClasses)
            {
                _typeToIds.Add(subClass, id);
                _idsToType.Add(id, subClass);
                id++;
            }

            _buffer = buffer;
        }

        private Dictionary<NetworkId, NetworkObject> _netObjects = new Dictionary<NetworkId, NetworkObject>();

        private NetworkBuffer _buffer;

        public T Spawn<T>() where T : NetworkObject, new()
        {
            var newObj = new T();
            newObj.SetIdInternal(new NetworkId());
            newObj.SetActiveInternal(true);
            _netObjects.Add(newObj.Id, newObj);
            _buffer.Write(SpawnEncode(typeof(T), newObj.Id));
            return newObj;
        }

        public NetworkObject Spawn(Type t)
        {
            if (!_typeToIds.ContainsKey(t))
                throw new ArgumentException("The given type must inherit from NetworkObject.");
            
            var newObj = (NetworkObject?)Activator.CreateInstance(t);

            if (newObj == null)
                throw new InvalidOperationException("Failed to create new instance.");
            
            newObj.SetIdInternal(new NetworkId());
            newObj.SetActiveInternal(true);
            _netObjects.Add(newObj.Id, newObj);

            _buffer.Write(SpawnEncode(t, newObj.Id));

            return newObj;
        }

        private void ReceiveSpawn(Type t, NetworkId id)
        {
            if (!_typeToIds.ContainsKey(t))
                throw new ArgumentException("The given type must inherit from NetworkObject.");
            
            var newObj = (NetworkObject?)Activator.CreateInstance(t);

            if (newObj == null)
                throw new InvalidOperationException("Failed to create new instance.");
            
            newObj.SetIdInternal(id);
            newObj.SetActiveInternal(true);
            _netObjects.Add(newObj.Id, newObj);
        }

        private static byte[] SpawnEncode(Type objType, NetworkId id)
        {
            var bytes = new byte[]{RpcProtocol.NETWORK_OBJECT_NEW, _typeToIds[objType], 0, 0, 0, 0};
            var ind = 2;
            id.InsertBytes(ref bytes, ref ind);
            return bytes;
        }

        public void Destroy(NetworkObject target)
        {
            _netObjects.Remove(target.Id);
            target.SetActiveInternal(false);
            _buffer.Write(DestroyEncode(target.Id));
        }

        private void ReceiveDestroy(NetworkId id)
        {
            var target = _netObjects[id];
            _netObjects.Remove(id);
            target.SetActiveInternal(false);
        }

        private static byte[] DestroyEncode(NetworkId id)
        {
            var bytes = new byte[]{RpcProtocol.NETWORK_OBJECT_DESTROY, 0, 0, 0, 0};
            var ind = 1;
            id.InsertBytes(ref bytes, ref ind);
            return bytes;
        }

        public void Decode(byte[] message)
        {
            switch(message[0])
            {
                case RpcProtocol.NETWORK_OBJECT_NEW:
                    var objType = _idsToType[message[1]];
                    var id = new NetworkId(message, 2);
                    ReceiveSpawn(objType, id);
                    break;
                case RpcProtocol.NETWORK_OBJECT_DESTROY:
                    ReceiveDestroy(new NetworkId(message, 1));
                    break;
            }
        }
    }
}