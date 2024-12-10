using System;
using System.Collections.Generic;
using System.Net;

namespace OwlTree
{
    /// <summary>
    /// Manages a list of received connection requests. Requests have a lifetime that will 
    /// result in timeout if exceeded.
    /// </summary>
    internal class ConnectionRequestList
    {
        private int _maxClients;
        private long _requestTimeout;

        private List<(IPEndPoint endPoint, long timestamp)> _requests = new();

        public ConnectionRequestList(int maxClients, long requestTimeout)
        {
            _maxClients = maxClients;
            _requestTimeout = requestTimeout;
        }

        public int Count => _requests.Count;

        public void Add(IPEndPoint endPoint)
        {
            if (_requests.Count >= _maxClients)
                throw new InvalidOperationException("Cannot wait for more than " + _maxClients + " connection requests.");
            _requests.Add((endPoint, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        }

        /// <summary>
        /// Returns true if the given IP was in the waiting list. Also returns the port number of 
        /// end point that was originally added to the list.
        /// </summary>
        public bool TryGet(IPEndPoint endPoint, out int port)
        {
            for (int i = 0; i < _requests.Count; i++)
            {
                if (_requests[i].endPoint.Address.Equals(endPoint.Address))
                {
                    port = _requests[i].endPoint.Port;
                    _requests.RemoveAt(i);
                    return true;
                }
            }
            port = 0;
            return false;
        }

        /// <summary>
        /// Check all currently waiting requests for timeouts, removes requests that have timed-out.
        /// </summary>
        public void ClearTimeouts()
        {
            var time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            for (int i = 0; i < _requests.Count; i++)
            {
                if (time - _requests[i].timestamp >= _requestTimeout)
                {
                    _requests.RemoveAt(i);
                    i--;
                }
            }
        }
    }
}