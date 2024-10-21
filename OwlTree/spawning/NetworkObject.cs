
using System;
using System.Collections.Generic;
using System.Linq;

namespace OwlTree
{
    /// <summary>
    /// Base class for any object type that can be synchronously spawned.
    /// </summary>
    public class NetworkObject
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

        internal Action<ClientId, RpcId, NetworkId, object[]> OnRpcCall;

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
        public Connection Connection { get; private set; }

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
        public virtual void OnDespawn() { }
    }
}