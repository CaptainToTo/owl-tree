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

        private uint _curId = NetworkId.FirstNetworkId;
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

            newObj.Id = NextNetworkId();
            newObj.IsActive = true;
            newObj.Connection = _connection;
            newObj.i_OnRpcCall = _connection.AddRpc;
            _netObjects.Add(newObj.Id, newObj);
            newObj.InitializeSimulatedProperties();

            _connection.AddOutgoingMessage(EncodeSpawn(ClientId.None, typeof(T), newObj.Id));

            newObj.OnSpawn();
            OnObjectSpawn?.Invoke(newObj);
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
            
            newObj.Id = NextNetworkId();
            newObj.IsActive = true;
            newObj.Connection = _connection;
            newObj.i_OnRpcCall = _connection.AddRpc;
            _netObjects.Add(newObj.Id, newObj);
            newObj.InitializeSimulatedProperties();

            _connection.AddOutgoingMessage(EncodeSpawn(ClientId.None, t, newObj.Id));

            newObj.OnSpawn();
            OnObjectSpawn?.Invoke(newObj);

            return newObj;
        }

        public void SendNetworkObjects(ClientId callee)
        {
            foreach (var pair in _netObjects)
            {
                _connection.AddOutgoingMessage(EncodeSpawn(callee, pair.Value.GetType(), pair.Key));
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
            
            newObj.Id = id;
            newObj.IsActive = true;
            newObj.Connection = _connection;
            newObj.i_OnRpcCall = _connection.AddRpc;
            _netObjects.Add(newObj.Id, newObj);
            newObj.InitializeSimulatedProperties();
            newObj.OnSpawn();
            OnObjectSpawn?.Invoke(newObj);
        }

        internal static int SpawnByteLength => RpcId.MaxByteLength + 1 + NetworkId.MaxByteLength;

        // encodes spawn into byte array for send
        internal OutgoingMessage EncodeSpawn(ClientId callee, Type objType, NetworkId id)
        {
            var m = new OutgoingMessage{
                tick = _connection.LocalTick,
                caller = _connection.LocalId,
                callee = callee,
                rpcId = new RpcId(RpcId.NetworkObjectSpawnId),
                protocol = Protocol.Tcp,
                perms = RpcPerms.AuthorityToClients,
                bytes = new byte[SpawnByteLength]
            };

            int ind = 0;
            var bytes = m.bytes.AsSpan();

            m.rpcId.InsertBytes(bytes);
            ind += m.rpcId.ByteLength();

            bytes[ind] = _proxyFactory?.TypeId(objType) ?? 0;
            ind += 1;

            var idSpan = bytes.Slice(ind, id.ByteLength());
            id.InsertBytes(idSpan);

            return m;
        }

        internal string SpawnEncodingSummary(ClientId callee, Type objType, NetworkId id)
        {
            string title = "Spawn Network Object of type <" + objType.ToString() + "> w/ Id " + id.ToString() + ":\n";
            var bytes = EncodeSpawn(callee, objType, id).bytes;
            string bytesStr = "     Bytes: " + BitConverter.ToString(bytes) + "\n";
            string encoding = "  Encoding: |__RpcId__| NT |__NetId__|";
            return title + bytesStr + encoding;
        }

        internal string SpawnEncodingSummary(byte objType, NetworkId id)
        {
            return SpawnEncodingSummary(_connection.LocalId, _proxyFactory?.TypeFromId(objType) ?? typeof(NetworkObject), id);
        }

        /// <summary>
        /// Destroy the given NetworkObject across all clients.
        /// </summary>
        public void Despawn(NetworkObject target)
        {
            _netObjects.Remove(target.Id);
            target.IsActive = false;
            _connection.AddOutgoingMessage(EncodeDespawn(target.Id));
            target.OnDespawn();
            OnObjectDespawn?.Invoke(target);
        }

        // run destroy on client
        private void ReceiveDespawn(NetworkId id)
        {
            var target = _netObjects[id];
            _netObjects.Remove(id);
            target.IsActive = false;
            target.OnDespawn();
            OnObjectDespawn?.Invoke(target);
        }

        public void DespawnAll()
        {
            var netObjs = _netObjects.Values;
            foreach (var obj in netObjs)
            {
                obj.IsActive = false;
                obj.OnDespawn();
            }
            _netObjects.Clear();
        }

        internal static int DespawnByteLength => RpcId.MaxByteLength + NetworkId.MaxByteLength;

        // encodes destroy into byte array for send
        internal OutgoingMessage EncodeDespawn(NetworkId id)
        {
            var m = new OutgoingMessage{
                tick = _connection.LocalTick,
                caller = _connection.LocalId,
                callee = ClientId.None,
                rpcId = new RpcId(RpcId.NetworkObjectDespawnId),
                protocol = Protocol.Tcp,
                perms = RpcPerms.AuthorityToClients,
                bytes = new byte[DespawnByteLength]
            };

            var ind = 0;
            var bytes = m.bytes.AsSpan();

            m.rpcId.InsertBytes(bytes);
            ind += m.rpcId.ByteLength();

            id.InsertBytes(bytes.Slice(ind));

            return m;
        }

        internal string DespawnEncodingSummary(NetworkId id)
        {
            string title = "Despawn Network Object " + id.ToString() + ":\n";
            byte[] bytes = EncodeDespawn(id).bytes;
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
                case RpcId.NetworkObjectSpawnId:
                    var objType = _proxyFactory?.TypeFromId((byte)args[0]) ?? typeof(NetworkObject);
                    var id = (NetworkId)args[1];
                    ReceiveSpawn(objType, id);
                    break;
                case RpcId.NetworkObjectDespawnId:
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
                case RpcId.NetworkObjectSpawnId:
                    ind += 1;
                    args = new object[]{message[RpcId.MaxByteLength], new NetworkId(message.Slice(rpcId.ByteLength() + 1))};
                    break;
                case RpcId.NetworkObjectDespawnId:
                    args = new object[]{new NetworkId(message.Slice(rpcId.ByteLength()))};
                    break;
                default:
                    return false;
            }
            return true;
        }
    }
}