
namespace OwlTree
{
    /// <summary>
    /// Unique integer Id for each client connected to the server. Id are unique for each connection.
    /// This means if a client disconnects and then reconnects, their PlayerId will be different.
    /// </summary>
    public struct PlayerId
    {
        // tracks the current id for the next id generated
        private static UInt32 _curId = 0;

        /// <summary>
        /// Reset ids. Provide an array of current client ids, which will be re-assigned to reduce the max id value.
        /// Use this for long-running servers which might run out available ids. Any PlayerIds that are stored as integers 
        /// will likely be inaccurate after a reset.<br />
        /// <br />
        /// newIds must be at least the same length as curIds.
        /// </summary>
        public static void ResetIdsNonAlloc(PlayerId[] curIds, ref PlayerId[] newIds)
        {
            if (curIds.Length > newIds.Length)
                throw new ArgumentException("The newIds array must be at least the same size as the curIds array.");

            _curId = 0;
            for (int i = 0; i < curIds.Length; i++)
            {
                newIds[i]._id = _curId;
                _curId++;
            }
        }

        /// <summary>
        /// Reset ids. Provide an array of current client ids, which will be re-assigned to reduce the max id value.
        /// Use this for long-running servers which might run out available ids. Any PlayerIds that are stored as integers 
        /// will likely be inaccurate after a reset.
        /// </summary>
        public static PlayerId[] ResetIds(PlayerId[] curIds)
        {
            PlayerId[] newIds = new PlayerId[curIds.Length];
            ResetIdsNonAlloc(curIds, ref newIds);
            return newIds;
        }

        // the actual id
        private UInt32 _id;

        /// <summary>
        /// Generate a new player id.
        /// </summary>
        public PlayerId()
        {
            _id = _curId;
            _curId++;
        }

        /// <summary>
        /// Get a PlayerId instance using an existing id.
        /// </summary>
        public PlayerId(uint id)
        {
            if (id >= _curId)
                throw new ArgumentException("Ids must be for an already generated player id.");
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

        // Operators

        public static bool operator ==(PlayerId a, PlayerId b)
        {
            return a._id == b._id;
        }

        public static bool operator !=(PlayerId a, PlayerId b)
        {
            return a._id != b._id;
        }

        public override bool Equals(object? obj)
        {
            return obj != null && obj.GetType() == typeof(PlayerId) && ((PlayerId)obj)._id == _id;
        }

        public override int GetHashCode()
        {
            return _id.GetHashCode();
        }
    }
}