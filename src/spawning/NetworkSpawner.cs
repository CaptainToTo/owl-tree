namespace OwlTree
{
    /// <summary>
    /// Manages NetworkObject spawning and destroying. Operations can only be triggered by the server.
    /// </summary>
    public class NetworkSpawner
    {
        // map NetworkObject sub-types to specific encodings
        private static Dictionary<Type, byte> _typeToIds = new Dictionary<Type, byte>();
        private static Dictionary<byte, Type> _idsToType = new Dictionary<byte, Type>();

        /// <summary>
        /// Initialize spawner, requires a NetworkBuffer for sending spawn and destroy messages.
        /// </summary>
        public NetworkSpawner(Connection connection, NetworkBuffer buffer)
        {
            byte id = 1;
            _typeToIds.Add(typeof(NetworkObject), id);
            _idsToType.Add(id, typeof(NetworkObject));

            id += 1;

            var subClasses = NetworkObject.GetNetworkObjectTypes();

            foreach (var subClass in subClasses)
            {
                if (_typeToIds.ContainsKey(subClass)) continue;
                _typeToIds.Add(subClass, id);
                _idsToType.Add(id, subClass);
                id++;
            }

            _connection = connection;
            _buffer = buffer;
        }

        // all active network objects
        private Dictionary<NetworkId, NetworkObject> _netObjects = new Dictionary<NetworkId, NetworkObject>();

        /// <summary>
        /// Try to get an object with the given id. Returns true if one was found, false otherwise.
        /// </summary>
        public bool TryGetObject(NetworkId id, out NetworkObject? obj)
        {
            return _netObjects.TryGetValue(id, out obj);
        }

        /// <summary>
        /// Get an object with the given id. Returns null if none exist.
        /// </summary>
        public NetworkObject? GetNetworkObject(NetworkId id)
        {
            if (!_netObjects.ContainsKey(id))
                return null;
            return _netObjects[id];
        }

        private NetworkBuffer _buffer;
        private Connection _connection;

        /// <summary>
        /// Invoked when a new object is spawned. Provides the spawned object. 
        /// Invoked after the object's OnSpawn() method has been called.
        /// </summary>
        public NetworkObject.Delegate? OnObjectSpawn;

        /// <summary>
        /// Invoked when an object is destroyed. Provides the "destroyed" object, marked as not active.
        /// Invoked after the object's OnDestroy() method has been called.
        /// </summary>
        public NetworkObject.Delegate? OnObjectDestroy;

        /// <summary>
        /// Spawns a new instance of the given NetworkObject sub-type across all clients.
        /// </summary>
        public T Spawn<T>() where T : NetworkObject, new()
        {
            var newObj = new T();
            newObj.SetIdInternal(new NetworkId());
            newObj.SetActiveInternal(true);
            newObj.SetConnectionInternal(_connection);
            _netObjects.Add(newObj.Id, newObj);
            _buffer.Write(SpawnEncode(typeof(T), newObj.Id));
            newObj.OnSpawn();
            OnObjectSpawn?.Invoke(newObj);
            return newObj;
        }

        /// <summary>
        /// Spawns a new instance of the given NetworkObject sub-type across all clients.
        /// </summary>
        public NetworkObject Spawn(Type t)
        {
            if (!_typeToIds.ContainsKey(t))
                throw new ArgumentException("The given type must inherit from NetworkObject.");
            
            var newObj = (NetworkObject?)Activator.CreateInstance(t);

            if (newObj == null)
                throw new InvalidOperationException("Failed to create new instance.");
            
            newObj.SetIdInternal(new NetworkId());
            newObj.SetActiveInternal(true);
            newObj.SetConnectionInternal(_connection);
            _netObjects.Add(newObj.Id, newObj);

            _buffer.Write(SpawnEncode(t, newObj.Id));

            newObj.OnSpawn();
            OnObjectSpawn?.Invoke(newObj);

            return newObj;
        }

        // run spawn on client
        private void ReceiveSpawn(Type t, NetworkId id)
        {
            if (!_typeToIds.ContainsKey(t))
                throw new ArgumentException("The given type must inherit from NetworkObject.");
            
            var newObj = (NetworkObject?)Activator.CreateInstance(t);

            if (newObj == null)
                throw new InvalidOperationException("Failed to create new instance.");
            
            newObj.SetIdInternal(id);
            newObj.SetActiveInternal(true);
            newObj.SetConnectionInternal(_connection);
            _netObjects.Add(newObj.Id, newObj);
            newObj.OnSpawn();
            OnObjectSpawn?.Invoke(newObj);
        }

        // encodes spawn into byte array for send
        private static byte[] SpawnEncode(Type objType, NetworkId id)
        {
            var bytes = new byte[]{RpcProtocol.NETWORK_OBJECT_NEW, _typeToIds[objType], 0, 0, 0, 0};
            var ind = 2;
            id.InsertBytes(ref bytes, ref ind);
            return bytes;
        }

        /// <summary>
        /// Destroy the given NetworkObject across all clients.
        /// </summary>
        public void Destroy(NetworkObject target)
        {
            _netObjects.Remove(target.Id);
            target.SetActiveInternal(false);
            _buffer.Write(DestroyEncode(target.Id));
            target.OnDestroy();
            OnObjectDestroy?.Invoke(target);
        }

        // run destroy on client
        private void ReceiveDestroy(NetworkId id)
        {
            var target = _netObjects[id];
            _netObjects.Remove(id);
            target.SetActiveInternal(false);
            target.OnDestroy();
            OnObjectDestroy?.Invoke(target);
        }

        // encodes destroy into byte array for send
        private static byte[] DestroyEncode(NetworkId id)
        {
            var bytes = new byte[]{RpcProtocol.NETWORK_OBJECT_DESTROY, 0, 0, 0, 0};
            var ind = 1;
            id.InsertBytes(ref bytes, ref ind);
            return bytes;
        }

        /// <summary>
        /// Decodes the given message, assuming it is either a spawn or destroy instruction from the server.
        /// If decoded, the spawn or destroy instruction will be executed.
        /// </summary>
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