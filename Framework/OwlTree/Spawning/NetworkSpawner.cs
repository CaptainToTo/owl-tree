using System;
using System.Collections.Generic;
using System.Linq;

namespace OwlTree
{
    /// <summary>
    /// Manages NetworkObject spawning and destroying. Operations can only be triggered by the server.
    /// </summary>
    public class NetworkSpawner
    {
        /// <summary>
        /// Initialize spawner, requires a NetworkBuffer for sending spawn and destroy messages.
        /// </summary>
        public NetworkSpawner(Connection connection, ProxyFactory proxyFactory)
        {
            _connection = connection;
            _proxyFactory = proxyFactory;
        }

        // all active network objects
        private Dictionary<NetworkId, NetworkObject> _netObjects = new Dictionary<NetworkId, NetworkObject>();

        /// <summary>
        /// Iterable of all currently spawned network objects
        /// </summary>
        public IEnumerable<NetworkObject> NetworkObjects => _netObjects.Values;

        /// <summary>
        /// Try to get an object with the given id. Returns true if one was found, false otherwise.
        /// </summary>
        public bool TryGetObject(NetworkId id, out NetworkObject obj)
        {
            return _netObjects.TryGetValue(id, out obj);
        }

        /// <summary>
        /// Get an object with the given id. Returns null if none exist.
        /// </summary>
        public NetworkObject GetNetworkObject(NetworkId id)
        {
            if (!_netObjects.ContainsKey(id))
                return null;
            return _netObjects[id];
        }

        private Connection _connection;
        private ProxyFactory _proxyFactory;

        /// <summary>
        /// Invoked when a new object is spawned. Provides the spawned object. 
        /// Invoked after the object's OnSpawn() method has been called.
        /// </summary>
        public NetworkObject.Delegate OnObjectSpawn;

        /// <summary>
        /// Invoked when an object is despawned. Provides the "despawned" object, marked as not active.
        /// Invoked after the object's OnDespawn() method has been called.
        /// </summary>
        public NetworkObject.Delegate OnObjectDespawn;

        private uint _curId = NetworkId.FIRST_NETWORK_ID;
        private NetworkId NextNetworkId()
        {
            var id = new NetworkId(_curId);
            _curId++;
            return id;
        }

        /// <summary>
        /// Spawns a new instance of the given NetworkObject sub-type across all clients.
        /// If the given type is not a known NetworkObject type, throws an error.
        /// </summary>
        public T Spawn<T>() where T : NetworkObject, new()
        {
            var newObj = (T)_proxyFactory?.CreateProxy(typeof(T));
            if (newObj == null)
                throw new InvalidOperationException("Failed to create new instance.");

            newObj.SetIdInternal(NextNetworkId());
            newObj.SetActiveInternal(true);
            newObj.SetConnectionInternal(_connection);
            newObj.i_OnRpcCall = _connection.AddRpc;
            _netObjects.Add(newObj.Id, newObj);
            newObj.OnSpawn();
            OnObjectSpawn?.Invoke(newObj);
            _connection.AddRpc(new RpcId(RpcId.NETWORK_OBJECT_SPAWN), new object[]{typeof(T), newObj.Id});
            return newObj;
        }

        /// <summary>
        /// Spawns a new instance of the given NetworkObject sub-type across all clients.
        /// If the given type is not a known NetworkObject type, throws an error.
        /// </summary>
        public NetworkObject Spawn(Type t)
        {
            if (!(_proxyFactory?.HasTypeId(t) ?? false))
                throw new ArgumentException("The given type must inherit from NetworkObject.");
            
            var newObj = _proxyFactory?.CreateProxy(t);

            if (newObj == null)
                throw new InvalidOperationException("Failed to create new instance.");
            
            newObj.SetIdInternal(NextNetworkId());
            newObj.SetActiveInternal(true);
            newObj.SetConnectionInternal(_connection);
            newObj.i_OnRpcCall = _connection.AddRpc;
            _netObjects.Add(newObj.Id, newObj);

            _connection.AddRpc(new RpcId(RpcId.NETWORK_OBJECT_SPAWN), new object[]{t, newObj.Id});

            newObj.OnSpawn();
            OnObjectSpawn?.Invoke(newObj);

            return newObj;
        }

        public void SendNetworkObjects(ClientId callee)
        {
            foreach (var pair in _netObjects)
            {
                _connection.AddRpc(callee, new RpcId(RpcId.NETWORK_OBJECT_SPAWN), Protocol.Tcp, new object[]{pair.Value.GetType(), pair.Key});
            }
        }

        // run spawn on client
        private void ReceiveSpawn(Type t, NetworkId id)
        {
            if (!(_proxyFactory?.HasTypeId(t) ?? false))
                throw new ArgumentException("The given type must inherit from NetworkObject.");

            if (_netObjects.ContainsKey(id))
                return;
            
            var newObj = _proxyFactory?.CreateProxy(t);

            if (newObj == null)
                throw new InvalidOperationException("Failed to create new instance.");
            
            if (_curId <= id.Id)
                _curId = id.Id + 1;
            
            newObj.SetIdInternal(id);
            newObj.SetActiveInternal(true);
            newObj.SetConnectionInternal(_connection);
            newObj.i_OnRpcCall = _connection.AddRpc;
            _netObjects.Add(newObj.Id, newObj);
            newObj.OnSpawn();
            OnObjectSpawn?.Invoke(newObj);
        }

        internal static int SpawnByteLength { get { return RpcId.MaxLength() + 1 + NetworkId.MaxLength(); } }

        // encodes spawn into byte array for send
        internal void SpawnEncode(Span<byte> bytes, Type objType, NetworkId id)
        {
            int ind = 0;

            var rpcId = new RpcId(RpcId.NETWORK_OBJECT_SPAWN);
            var rpcSpan = bytes.Slice(ind, rpcId.ByteLength());
            rpcId.InsertBytes(rpcSpan);
            ind += rpcId.ByteLength();

            bytes[rpcId.ByteLength()] = _proxyFactory?.TypeId(objType) ?? 0;
            ind += 1;

            var idSpan = bytes.Slice(ind, id.ByteLength());
            id.InsertBytes(idSpan);
        }

        internal string SpawnEncodingSummary(Type objType, NetworkId id)
        {
            string title = "Spawn Network Object of type <" + objType.ToString() + "> w/ Id " + id.ToString() + ":\n";
            byte[] bytes = new byte[SpawnByteLength];
            SpawnEncode(bytes.AsSpan(), objType, id);
            string bytesStr = "     Bytes: " + BitConverter.ToString(bytes) + "\n";
            string encoding = "  Encoding: |__RpcId__| NT |__NetId__|";
            return title + bytesStr + encoding;
        }

        internal string SpawnEncodingSummary(byte objType, NetworkId id)
        {
            return SpawnEncodingSummary(_proxyFactory?.TypeFromId(objType) ?? typeof(NetworkObject), id);
        }

        /// <summary>
        /// Destroy the given NetworkObject across all clients.
        /// </summary>
        public void Despawn(NetworkObject target)
        {
            _netObjects.Remove(target.Id);
            target.SetActiveInternal(false);
            _connection.AddRpc(new RpcId(RpcId.NETWORK_OBJECT_DESPAWN), new object[]{target.Id});
            target.OnDespawn();
            OnObjectDespawn?.Invoke(target);
        }

        // run destroy on client
        private void ReceiveDespawn(NetworkId id)
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

        internal static int DespawnByteLength { get { return RpcId.MaxLength() + NetworkId.MaxLength(); } }

        // encodes destroy into byte array for send
        internal void DespawnEncode(Span<byte> bytes, NetworkId id)
        {
            var ind = 0;

            var rpcId = new RpcId(RpcId.NETWORK_OBJECT_DESPAWN);
            var rpcSpan = bytes.Slice(0, rpcId.ByteLength());
            rpcId.InsertBytes(rpcSpan);
            ind += rpcId.ByteLength();

            var idSpan = bytes.Slice(ind, id.ByteLength());
            id.InsertBytes(idSpan);
        }

        internal string DespawnEncodingSummary(NetworkId id)
        {
            string title = "Despawn Network Object " + id.ToString() + ":\n";
            byte[] bytes = new byte[DespawnByteLength];
            DespawnEncode(bytes.AsSpan(), id);
            string bytesStr = "     Bytes: " + BitConverter.ToString(bytes) + "\n";
            string encoding = "  Encoding: |__RpcId__| |__NetId__|";
            return title + bytesStr + encoding;
        }

        /// <summary>
        /// Decodes the given message, assuming it is either a spawn or destroy instruction from the server.
        /// If decoded, the spawn or destroy instruction will be executed.
        /// </summary>
        internal void ReceiveInstruction(RpcId rpcId, object[] args)
        {
            if (args == null) return;
            switch(rpcId.Id)
            {
                case RpcId.NETWORK_OBJECT_SPAWN:
                    var objType = _proxyFactory?.TypeFromId((byte)args[0]) ?? typeof(NetworkObject);
                    var id = (NetworkId)args[1];
                    ReceiveSpawn(objType, id);
                    break;
                case RpcId.NETWORK_OBJECT_DESPAWN:
                    ReceiveDespawn((NetworkId)args[0]);
                    break;
            }
        }

        internal static bool TryDecode(ReadOnlySpan<byte> message, out RpcId rpcId, out object[] args)
        {
            args = null;
            int ind = 0;
            rpcId = new RpcId(message);
            switch(rpcId.Id)
            {
                case RpcId.NETWORK_OBJECT_SPAWN:
                    ind += 1;
                    args = new object[]{message[RpcId.MaxLength()], new NetworkId(message.Slice(rpcId.ByteLength() + 1))};
                    break;
                case RpcId.NETWORK_OBJECT_DESPAWN:
                    args = new object[]{new NetworkId(message.Slice(rpcId.ByteLength()))};
                    break;
                default:
                    return false;
            }
            return true;
        }
    }
}