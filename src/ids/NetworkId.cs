namespace OwlTree
{
    /// <summary>
    /// Unique integer Id for each network object. 
    /// </summary>
    public struct NetworkId
    {
        // tracks the current id for the next id generated
        private static UInt32 _curId = 1;

        /// <summary>
        /// Reset ids. Provide an array of all current network object ids, which will be re-assigned to reduce the max id value.
        /// Use this for long-running servers which might run out available ids. Any NetworkIds that are stored as integers 
        /// will likely be inaccurate after a reset.<br />
        /// <br />
        /// newIds must be at least the same length as curIds.
        /// </summary>
        public static void ResetIdsNonAlloc(NetworkId[] curIds, ref NetworkId[] newIds)
        {
            if (curIds.Length > newIds.Length)
                throw new ArgumentException("The newIds array must be at least the same size as the curIds array.");

            _curId = 1;
            for (int i = 0; i < curIds.Length; i++)
            {
                newIds[i]._id = _curId;
                _curId++;
            }
        }

        /// <summary>
        /// Reset ids. Provide an array of all current network object ids, which will be re-assigned to reduce the max id value.
        /// Use this for long-running servers which might run out available ids. Any NetworkIds that are stored as integers 
        /// will likely be inaccurate after a reset.
        /// </summary>
        public static NetworkId[] ResetIds(NetworkId[] curIds)
        {
            NetworkId[] newIds = new NetworkId[curIds.Length];
            ResetIdsNonAlloc(curIds, ref newIds);
            return newIds;
        }

        // the actual id
        private UInt32 _id;

        /// <summary>
        /// Generate a new network object id.
        /// </summary>
        public NetworkId()
        {
            _id = _curId;
            _curId++;
        }

        /// <summary>
        /// Get a NetworkId instance using an existing id.
        /// </summary>
        public NetworkId(uint id)
        {
            if (id >= _curId)
                throw new ArgumentException("Ids must be for an already generated network object id.");
            _id = id;
        }

        /// <summary>
        /// The id value.
        /// </summary>
        public uint Id { get { return _id; } }

        /// <summary>
        /// True if this id has a valid value.
        /// </summary>
        public bool IsValid { get { return _id < _curId; } }

        /// <summary>
        /// The network object id used to signal that there is no object. Id value is 0.
        /// </summary>
        public static NetworkId None = new NetworkId(0);

        // Operators

        public static bool operator ==(NetworkId a, NetworkId b)
        {
            return a._id == b._id;
        }

        public static bool operator !=(NetworkId a, NetworkId b)
        {
            return a._id != b._id;
        }

        public override bool Equals(object? obj)
        {
            return obj != null && obj.GetType() == typeof(NetworkId) && ((NetworkId)obj)._id == _id;
        }

        public override int GetHashCode()
        {
            return _id.GetHashCode();
        }
    }
}