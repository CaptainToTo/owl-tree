
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace OwlTree.Matchmaking
{
    /// <summary>
    /// Run on the server to listen for matchmaking requests from a matchmaking client.
    /// </summary>
    public class MatchmakingEndpoint
    {
        /// <summary>
        /// Function signature used to inject request handling into the endpoint.
        /// </summary>
        public delegate MatchmakingResponse ProcessRequest(IPAddress client, MatchmakingRequest request);

        private HttpListener _listener;
        private ProcessRequest _callback;

        /// <summary>
        /// Create a new matchmaking endpoint that will listen to the given domain.
        /// The given callback will be invoked when a matchmaking request is received.
        /// </summary>
        public MatchmakingEndpoint(string domain, ProcessRequest processRequest)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(domain);
            _callback = processRequest;
        }

        /// <summary>
        /// Create a new matchmaking endpoint that will listen to the given domains.
        /// The given callback will be invoked when a matchmaking request is received.
        /// </summary>
        public MatchmakingEndpoint(IEnumerable<string> domains, ProcessRequest processRequest)
        {
            _listener = new HttpListener();
            foreach (var domain in domains)
                _listener.Prefixes.Add(domain);
            _callback = processRequest;
        }

        /// <summary>
        /// Returns true if endpoint is currently listening for matchmaking requests.
        /// </summary>
        public bool IsActive { get; private set; } = false;

        /// <summary>
        /// Start listening for matchmaking requests asynchronously.
        /// </summary>
        public async void Start()
        {
            _listener.Start();
            IsActive = true;

            while (IsActive)
            {
                var context = await _listener.GetContextAsync();
                var request = context.Request;
                var response = context.Response;
                var source = request.RemoteEndPoint.Address;

                if (request.Url?.AbsolutePath == "/matchmaking")
                {
                    try
                    {
                        string requestBody = new StreamReader(request.InputStream, Encoding.UTF8).ReadToEnd();
                        var requestObj = MatchmakingRequest.Deserialize(requestBody);
                        var responseObj = _callback.Invoke(source, requestObj);
                        string responseBody = responseObj.Serialize();
                        response.StatusCode = (int)responseObj.responseCode;
                        byte[] buffer = Encoding.UTF8.GetBytes(responseBody);
                        response.ContentLength64 = buffer.Length;
                        response.OutputStream.Write(buffer, 0, buffer.Length);
                    }
                    catch
                    {
                        response.StatusCode = (int)ResponseCodes.RequestRejected;
                    }
                }
                else
                {
                    response.StatusCode = (int)ResponseCodes.NotFound;
                }
                response.OutputStream.Close();
            }

            _listener.Close();
        }

        /// <summary>
        /// Close the endpoint.
        /// </summary>
        public void Close()
        {
            IsActive = false;
        }
    }
}