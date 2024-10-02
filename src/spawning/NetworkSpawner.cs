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
        public NetworkSpawner(Connection connection)
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

        private Connection _connection;

        /// <summary>
        /// Invoked when a new object is spawned. Provides the spawned object. 
        /// Invoked after the object's OnSpawn() method has been called.
        /// </summary>
        public NetworkObject.Delegate? OnObjectSpawn;

        /// <summary>
        /// Invoked when an object is despawned. Provides the "despawned" object, marked as not active.
        /// Invoked after the object's OnDespawn() method has been called.
        /// </summary>
        public NetworkObject.Delegate? OnObjectDespawn;

        /// <summary>
        /// Spawns a new instance of the given NetworkObject sub-type across all clients.
        /// </summary>
        public T Spawn<T>() where T : NetworkObject, new()
        {
            var newObj = new T();
            newObj.SetIdInternal(new NetworkId());
            newObj.SetActiveInternal(true);
            newObj.SetConnectionInternal(_connection);
            newObj.OnRpcCall = _connection.AddRpc;
            _netObjects.Add(newObj.Id, newObj);
            _connection.AddRpc(new RpcId(RpcId.NETWORK_OBJECT_SPAWN), [typeof(T), newObj.Id]);
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
            newObj.OnRpcCall = _connection.AddRpc;
            _netObjects.Add(newObj.Id, newObj);

            _connection.AddRpc(new RpcId(RpcId.NETWORK_OBJECT_SPAWN), [t, newObj.Id]);

            newObj.OnSpawn();
            OnObjectSpawn?.Invoke(newObj);

            return newObj;
        }

        public void SendNetworkObjects(ClientId callee)
        {
            foreach (var pair in _netObjects)
            {
                _connection.AddRpc(callee, new RpcId(RpcId.NETWORK_OBJECT_SPAWN), [pair.Value.GetType(), pair.Key]);
            }
        }

        // run spawn on client
        private void ReceiveSpawn(Type t, NetworkId id)
        {
            if (!_typeToIds.ContainsKey(t))
                throw new ArgumentException("The given type must inherit from NetworkObject.");

            if (_netObjects.ContainsKey(id))
                return;
            
            var newObj = (NetworkObject?)Activator.CreateInstance(t);

            if (newObj == null)
                throw new InvalidOperationException("Failed to create new instance.");
            
            newObj.SetIdInternal(id);
            newObj.SetActiveInternal(true);
            newObj.SetConnectionInternal(_connection);
            newObj.OnRpcCall = _connection.AddRpc;
            _netObjects.Add(newObj.Id, newObj);
            newObj.OnSpawn();
            OnObjectSpawn?.Invoke(newObj);
        }

        public static int SpawnByteLength { get { return RpcId.MaxLength() + 1 + NetworkId.MaxLength(); } }

        // encodes spawn into byte array for send
        public static void SpawnEncode(Span<byte> bytes, Type objType, NetworkId id)
        {
            int ind = 0;

            var rpcId = new RpcId(RpcId.NETWORK_OBJECT_SPAWN);
            var rpcSpan = bytes.Slice(ind, rpcId.ExpectedLength());
            rpcId.InsertBytes(rpcSpan);
            ind += rpcId.ExpectedLength();

            bytes[rpcId.ExpectedLength()] = _typeToIds[objType];
            ind += 1;

            var idSpan = bytes.Slice(ind, id.ExpectedLength());
            id.InsertBytes(idSpan);
        }

        /// <summary>
        /// Destroy the given NetworkObject across all clients.
        /// </summary>
        public void Despawn(NetworkObject target)
        {
            _netObjects.Remove(target.Id);
            target.SetActiveInternal(false);
            _connection.AddRpc(new RpcId(RpcId.NETWORK_OBJECT_DESPAWN), [target.Id]);
            target.OnDespawn();
            OnObjectDespawn?.Invoke(target);
        }

        // run destroy on client
        private void ReceiveDestroy(NetworkId id)
        {
            var target = _netObjects[id];
            _netObjects.Remove(id);
            target.SetActiveInternal(false);
            target.OnDespawn();
            OnObjectDespawn?.Invoke(target);
        }

        public void DespawnAll()
        {
            var netObjs = _netObjects.Values;
            foreach (var obj in netObjs)
            {
                obj.SetActiveInternal(false);
                _netObjects.Remove(obj.Id);
                obj.OnDespawn();
            }
            _netObjects.Clear();
        }

        public static int DespawnByteLength { get { return RpcId.MaxLength() + NetworkId.MaxLength(); } }

        // encodes destroy into byte array for send
        public static void DespawnEncode(Span<byte> bytes, NetworkId id)
        {
            var ind = 0;

            var rpcId = new RpcId(RpcId.NETWORK_OBJECT_DESPAWN);
            var rpcSpan = bytes.Slice(0, rpcId.ExpectedLength());
            rpcId.InsertBytes(rpcSpan);
            ind += rpcId.ExpectedLength();

            var idSpan = bytes.Slice(ind, id.ExpectedLength());
            id.InsertBytes(idSpan);
        }

        /// <summary>
        /// Decodes the given message, assuming it is either a spawn or destroy instruction from the server.
        /// If decoded, the spawn or destroy instruction will be executed.
        /// </summary>
        public void ReceiveInstruction(RpcId rpcId, object[]? args)
        {
            if (args == null) return;
            switch(rpcId.Id)
            {
                case RpcId.NETWORK_OBJECT_SPAWN:
                    var objType = _idsToType[(byte)args[0]];
                    var id = (NetworkId)args[1];
                    ReceiveSpawn(objType, id);
                    break;
                case RpcId.NETWORK_OBJECT_DESPAWN:
                    ReceiveDestroy((NetworkId)args[0]);
                    break;
            }
        }

        public static bool TryDecode(ReadOnlySpan<byte> message, out RpcId rpcId, out object[]? args)
        {
            args = null;
            int ind = 0;
            rpcId = new RpcId(message);
            switch(rpcId.Id)
            {
                case RpcId.NETWORK_OBJECT_SPAWN:
                    ind += 1;
                    args = new object[]{message[2], new NetworkId(message.Slice(rpcId.ExpectedLength() + 1))};
                    break;
                case RpcId.NETWORK_OBJECT_DESPAWN:
                    args = new object[]{new NetworkId(message.Slice(rpcId.ExpectedLength()))};
                    break;
                default:
                    return false;
            }
            return true;
        }
    }
}