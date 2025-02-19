
using System;
using System.Collections.Generic;

namespace OwlTree
{
    /// <summary>
    /// Tracks ping requests the local connection current has active.
    /// </summary>
    public class PingRequestList
    {
        private List<PingRequest> _requests = new();
        private int _requestTimeout;

        public PingRequestList(int timeout)
        {
            _requestTimeout = timeout;
        }

        /// <summary>
        /// Add a new ping request.
        /// </summary>
        public PingRequest Add(ClientId source, ClientId target)
        {
            var request = new PingRequest(source, target);
            _requests.Add(request);
            return request;
        }

        /// <summary>
        /// Searches for a ping request, given the target client id.
        /// Returns null if no such request is found.
        /// </summary>
        public PingRequest Find(ClientId target)
        {
            foreach (var r in _requests)
                if (r.Target == target) return r;
            return null;
        }

        public void Remove(PingRequest request)
        {
            _requests.Remove(request);
        }

        /// <summary>
        /// Check if any ping requests have expired, if so, call the given timeout handler,
        /// and remove the ping request.
        /// </summary>
        public void ClearTimeouts(PingRequest.Delegate timeoutHandler)
        {
            var time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            for (int i = 0; i < _requests.Count; i++)
            {
                if (time - _requests[i].SendTime >= _requestTimeout)
                {
                    timeoutHandler.Invoke(_requests[i]);
                    _requests.RemoveAt(i);
                    i--;
                }
            }
        }

    }
}