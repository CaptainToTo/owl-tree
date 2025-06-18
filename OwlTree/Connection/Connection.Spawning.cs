using System;
using System.Collections.Generic;

namespace OwlTree
{
    public partial class Connection
    {
        private NetworkSpawner _spawner;

        /// <summary>
        /// Invoked when a new object is spawned. Provides the spawned object. 
        /// Invoked after the object's <c>OnSpawn()</c> method has been called.
        /// </summary>
        public event NetworkObject.Delegate OnObjectSpawn;

        /// <summary>
        /// Invoked when an object is despawned. Provides the "despawned" object, marked as not active.
        /// Invoked after the object's <c>OnDespawn()</c> method has been called.
        /// </summary>
        public event NetworkObject.Delegate OnObjectDespawn;

        /// <summary>
        /// Iterable of all currently spawned network objects
        /// </summary>
        public IEnumerable<NetworkObject> NetworkObjects => IsRelay ? null : _spawner.NetworkObjects;

        /// <summary>
        /// Try to get an object with the given id. Returns true if one was found, false otherwise.
        /// </summary>
        public bool TryGetObject(NetworkId id, out NetworkObject obj)
        {
            if (IsRelay)
                throw new InvalidOperationException("Relay servers do not manage any state beyond client connections, no network objects exist on this connection.");
            return _spawner.TryGetObject(id, out obj);
        }

        /// <summary>
        /// Try to get an object of the given type, with the give id. Returns true if one was found, false otherwise.
        /// </summary>
        public bool TryGetObject<T>(NetworkId id, out T objT) where T : NetworkObject
        {
            if (IsRelay)
                throw new InvalidOperationException("Relay servers do not manage any state beyond client connections, no network objects exist on this connection.");
            if (_spawner.TryGetObject(id, out var obj) && obj is T)
            {
                objT = (T)obj;
                return true;
            }
            objT = null;
            return false;
        }

        /// <summary>
        /// Get an object with the given id. Returns null if none exist.
        /// </summary>
        public NetworkObject GetNetworkObject(NetworkId id)
        {
            if (IsRelay)
                throw new InvalidOperationException("Relay servers do not manage any state beyond client connections, no network objects exist on this connection.");
            return _spawner.GetNetworkObject(id);
        }

        /// <summary>
        /// Get an object with the given type and id. Returns null if none exists.
        /// </summary>
        public T GetNetworkObject<T>(NetworkId id) where T : NetworkObject
        {
            if (IsRelay)
                throw new InvalidOperationException("Relay servers do not manage any state beyond client connections, no network objects exist on this connection.");
            var obj = _spawner.GetNetworkObject(id);
            return obj is T ? (T)obj : null;
        }

        /// <summary>
        /// Spawns a new instance of the given NetworkObject sub-type across all clients.
        /// This can only be called by the authority.
        /// </summary>
        public T Spawn<T>() where T : NetworkObject, new()
        {
            if (IsClient)
                throw new InvalidOperationException("Clients cannot spawn or destroy network objects.");
            else if (IsRelay)
                throw new InvalidOperationException("Relay servers cannot spawn or destroy network objects.");

            try
            {
                var obj = _spawner.Spawn<T>();
                if (Logger.includes.rpcCallEncodings)
                    Logger.Write("SENDING:\n" + _spawner.SpawnEncodingSummary(ClientId.None, obj.GetType(), obj.Id));
                return obj;
            }
            catch (Exception e)
            {
                if (Logger.includes.exceptions)
                    Logger.Write($"FAILED to spawn new NetworkObject. Exception thrown:\n{e}");
                return null;
            }
        }

        /// <summary>
        /// Spawns a new instance of the given NetworkObject sub-type across all clients.
        /// This can only be called by the authority.
        /// </summary>
        public NetworkObject Spawn(Type t)
        {
            if (IsClient)
                throw new InvalidOperationException("Clients cannot spawn or despawn network objects");
            else if (IsRelay)
                throw new InvalidOperationException("Relay servers cannot spawn or destroy network objects.");
            try
            {
                var obj = _spawner.Spawn(t);
                if (Logger.includes.rpcCallEncodings)
                    Logger.Write("SENDING:\n" + _spawner.SpawnEncodingSummary(ClientId.None, obj.GetType(), obj.Id));
                return obj;
            }
            catch (Exception e)
            {
                if (Logger.includes.exceptions)
                    Logger.Write($"FAILED to spawn new NetworkObject. Exception thrown:\n{e}");
                return null;
            }
        }

        /// <summary>
        /// Despawns the given NetworkObject across all clients.
        /// This can only be called by the authority.
        /// </summary>
        public void Despawn(NetworkObject target)
        {
            if (IsClient)
                throw new InvalidOperationException("Clients cannot spawn or despawn network objects");
            else if (IsRelay)
                throw new InvalidOperationException("Relay servers cannot spawn or destroy network objects.");
            try
            {
                _spawner.Despawn(target);
                if (Logger.includes.rpcCallEncodings)
                    Logger.Write("SENDING:\n" + _spawner.DespawnEncodingSummary(target.Id));
            }
            catch (Exception e)
            {
                if (Logger.includes.exceptions)
                    Logger.Write($"FAILED to despawn NetworkObject. Exception thrown:\n{e}");
            }
        }

        /// <summary>
        /// Use to associate objects with key-value pairs, and make accessible to all
        /// objects with a reference to this connection. This is not synchronized across clients.
        /// Addons that use this may handle synchronization for you.
        /// </summary>
        public GenericObjectMaps Maps { get; private set; } = new();

        private interface ISearch
        {
            public object Id();
            public bool SearchForObject(Connection connection);
        }

        private List<ISearch> _idSearches = new();

        private struct IdSearch<K, V> : ISearch
        {
            public K id;
            public Action<V> callback;

            public IdSearch(K id, Action<V> callback)
            {
                this.id = id;
                this.callback = callback;
            }

            public object Id() => id;

            public bool SearchForObject(Connection connection)
            {
                if (connection.Maps.TryGet(id, out V obj))
                {
                    callback.Invoke(obj);
                    return true;
                }
                return false;
            }
        }

        private struct NetSearch : ISearch
        {
            public NetworkId id;
            public Action<NetworkObject> callback;

            public NetSearch(NetworkId id, Action<NetworkObject> callback)
            {
                this.id = id;
                this.callback = callback;
            }

            public object Id() => id;

            public bool SearchForObject(Connection connection)
            {
                if (connection.TryGetObject(id, out var obj))
                {
                    callback.Invoke(obj);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Enqueue a callback that will wait for a NetworkObject with the given NetworkId
        /// to exist.
        /// </summary>
        public void WaitForObject(NetworkId id, Action<NetworkObject> callback)
        {
            _idSearches.Add(new NetSearch(id, callback));
        }

        /// <summary>
        /// Enqueue a callback that will wait for a given key to exist in this connection's
        /// generic object maps.
        /// </summary>
        public void WaitForObject<K, V>(K id, Action<V> callback)
        {
            _idSearches.Add(new IdSearch<K, V>(id, callback));
        }

        // called in execute queue
        private void HandleSpawnerMessage(IncomingMessage message)
        {
            try
            {
                _spawner.ReceiveInstruction(message.rpcId, message.args);
            }
            catch (Exception e)
            {
                if (Logger.includes.exceptions)
                    Logger.Write($"Failed to run {(message.rpcId == RpcId.NetworkObjectSpawnId ? "spawn" : "despawn")} instruction. Exception thrown:\n   {e}");
            }
        }

        private void SearchForObjects()
        {
            for (int i = 0; i < _idSearches.Count; i++)
            {
                var search = _idSearches[i];
                try
                {
                    if (search.SearchForObject(this))
                    {
                        _idSearches.RemoveAt(i);
                        i--;
                    }
                }
                catch (Exception e)
                {
                    if (Logger.includes.exceptions)
                        Logger.Write($"FAILED to find object with id {search.Id()}, threw exception:\n{e}");
                }
            }
        }
    }
}