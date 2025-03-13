namespace OwlTree
{
    /// <summary>
    /// Use to label which protocol that will be used to send the message.
    /// </summary>
    public enum Protocol
    {
        Tcp,
        Udp
    }

    /// <summary>
    /// A message that has been received and decoded.
    /// </summary>
    public struct IncomingMessage
    {
        public delegate void Delegate(IncomingMessage m);

        /// <summary>
        /// The simulation tick this message was sent at. This will always be 0 if no simulation buffer is being managed.
        /// </summary>
        public Tick tick;

        /// <summary>
        /// Who sent the message. A caller of ClientId.None means it came from the server.
        /// </summary>
        public ClientId caller;

        /// <summary>
        /// Who should receive the message. A callee of ClientId.None means is should be sent to all sockets.
        /// </summary>
        public ClientId callee;

        /// <summary>
        /// The RPC this message is passing the arguments for.
        /// </summary>
        public RpcId rpcId;

        /// <summary>
        /// The NetworkId of the object that sent this message.
        /// </summary>
        public NetworkId target;

        /// <summary>
        /// Which protocol that will be used to send the message.
        /// </summary>
        public Protocol protocol;

        /// <summary>
        /// What the permission type is for this message.
        /// </summary>
        public RpcPerms perms;

        /// <summary>
        /// The arguments of the RPC call this message represents.
        /// </summary>
        public object[] args;
    }

    /// <summary>
    /// A message that will be sent and has been encoded.
    /// </summary>
    public struct OutgoingMessage
    {
        public delegate void Delegate(OutgoingMessage m);

        /// <summary>
        /// The simulation tick this message was sent at. This will always be 0 if no simulation buffer is being managed.
        /// </summary>
        public Tick tick;

        /// <summary>
        /// Who sent the message. A caller of ClientId.None means it came from the server.
        /// </summary>
        public ClientId caller;

        /// <summary>
        /// Who should receive the message. A callee of ClientId.None means is should be sent to all sockets.
        /// </summary>
        public ClientId callee;

        /// <summary>
        /// The RPC this message is passing the arguments for.
        /// </summary>
        public RpcId rpcId;

        /// <summary>
        /// The NetworkId of the object that sent this message.
        /// </summary>
        public NetworkId target;

        /// <summary>
        /// Which protocol that will be used to send the message.
        /// </summary>
        public Protocol protocol;

        /// <summary>
        /// What the permission type is for this message.
        /// </summary>
        public RpcPerms perms;

        /// <summary>
        /// The byte encoding of the message.
        /// </summary>
        public byte[] bytes;
    }
}