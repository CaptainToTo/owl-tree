
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

        /// <summary>
        /// The id reserved for signifying the base NetworkObject type.
        /// </summary>
        public const byte NetworkBaseTypeId = 1;
        /// <summary>
        /// The first valid id for derived network object types.
        /// </summary>
        public const byte FirstTypeId = 2;

        /// <summary>
        /// Basic function signature for passing NetworkObjects.
        /// </summary>
        public delegate void Delegate(NetworkObject obj);

        /// <summary>
        /// FOR INTERNAL USE ONLY. Broadcast an RPC call from this NetworkObject.
        /// <c>(callee id, rpc id, this network id, tcp or udp, args[])</c>
        /// </summary>
        public Action<ClientId, RpcId, NetworkId, Protocol, object[]> i_OnRpcCall { get; internal set; }

        /// <summary>
        /// The object's network id. This is synchronized across clients.
        /// </summary>
        public NetworkId Id { get; internal set; }

        /// <summary>
        /// Whether or not the object is currently being managed across clients. If false, 
        /// then the object has been "destroyed".
        /// </summary>
        public bool IsActive { get; internal set; }
        
        /// <summary>
        /// The connection this object associated with, and managed by.
        /// </summary>
        public Connection Connection { get; internal set; }

        /// <summary>
        /// FOR INTERNAL USE ONLY. Used to flag an object as receiving an RPC call from a remote source.
        /// </summary>
        public uint i_ReceivingRpc {get; internal set; } = 0;

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

        /// <summary>
        /// Returns the type of the user created NetworkObject subclass.
        /// To get the actual proxy object type, use <c>GetProxyType()</c>.
        /// </summary>
        public new virtual Type GetType() 
        { 
            return typeof(NetworkObject);
        }

        /// <summary>
        /// Returns the actual type of this proxy object.
        /// </summary>
        public virtual Type GetProxyType() 
        { 
            return typeof(NetworkObject);
        }
    }

    /// <summary>
    /// Manually assign an id value to derived NetworkObject types.
    /// This id is used for spawning new objects, based on type.
    /// Setting this manually ensures the id matches across different programs.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class AssignTypeIdAttribute : Attribute
    {
        public byte Id = 0;

        public AssignTypeIdAttribute(byte id)
        {
            Id = id;
        }
    }
}