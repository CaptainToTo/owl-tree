using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OwlTree.Matchmaking
{
    /// <summary>
    /// The kind of session the client is requesting.
    /// </summary>
    public enum ServerType
    {
        /// <summary>
        /// Requesting a server authoritative session.
        /// </summary>
        ServerAuthoritative,
        /// <summary>
        /// Requesting a relayed peer-to-peer session.
        /// </summary>
        Relay
    }
    
    /// <summary>
    /// What role the client is requesting.
    /// </summary>
    public enum ClientRole
    {
        /// <summary>
        /// Requesting to be a host in a relayed session.
        /// </summary>
        Host,
        /// <summary>
        /// Requesting to be a client in an existing session.
        /// </summary>
        Client
    }

    /// <summary>
    /// Sent by clients to a matchmaking endpoint.
    /// </summary>
    public struct MatchmakingRequest
    {
        /// <summary>
        /// The unique app id that will be used to verify clients attempting to 
        /// connect to the session.
        /// </summary>
        public string appId { get; set; }
        /// <summary>
        /// The unique session id that will identify the session from other sessions
        /// being managed by the server.
        /// </summary>
        public string sessionId { get; set; }
        /// <summary>
        /// The type of session being requested.
        /// </summary>
        public ServerType serverType { get; set; }
        /// <summary>
        /// The role this client is requesting.
        /// </summary>
        public ClientRole clientRole { get; set; }
        /// <summary>
        /// The max clients allowed at once in the requested session.
        /// </summary>
        public int maxClients { get; set; }
        /// <summary>
        /// Whether or not a relayed session will allow host migration.
        /// </summary>
        public bool migratable { get; set; }
        /// <summary>
        /// The version of OwlTree the session will use.
        /// </summary>
        public ushort owlTreeVersion { get; set; }
        /// <summary>
        /// The minimum version of OwlTree the session will allow.
        /// </summary>
        public ushort minOwlTreeVersion { get; set; }
        /// <summary>
        /// The version of your app the session will use.
        /// </summary>
        public ushort appVersion { get; set; }
        /// <summary>
        /// The minimum version of your app the session will allow.
        /// </summary>
        public ushort minAppVersion { get; set; }
        /// <summary>
        /// App specific arguments.
        /// </summary>
        public Dictionary<string, string> args { get; set; }

        /// <summary>
        /// Serialize the request as a JSON string.
        /// </summary>
        public string Serialize()
        {
            return JsonSerializer.Serialize(this);
        }

        /// <summary>
        /// Deserialize a request from a JSON string.
        /// </summary>
        public static MatchmakingRequest Deserialize(string data)
        {
            return JsonSerializer.Deserialize<MatchmakingRequest>(data);
        }
    }

    /// <summary>
    /// Use to send matchmaking requests to your matchmaking endpoint.
    /// </summary>
    public class MatchmakingClient
    {
        /// <summary>
        /// The domain this client will send requests to.
        /// </summary>
        public string EndpointDomain { get; private set; }

        /// <summary>
        /// Use to send matchmaking requests to your matchmaking endpoint.
        /// </summary>
        public MatchmakingClient(string endpointDomain)
        {
            EndpointDomain = endpointDomain;
        }

        /// <summary>
        /// Send a matchmaking request to the endpoint. Awaits a response that will contain
        /// data needed to create an OwlTree Connection.
        /// </summary>
        public async Task<MatchmakingResponse> MakeRequest(MatchmakingRequest request)
        {
            using var client = new HttpClient();

            try
            {
                var requestStr = request.Serialize();
                var content = new StringContent(requestStr, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(EndpointDomain + "/matchmaking", content);

                if (response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    return MatchmakingResponse.Deserialize(responseContent);
                }
                else
                {
                    switch ((int)response.StatusCode)
                    {
                        case (int)ResponseCodes.RequestRejected: return MatchmakingResponse.RequestRejected;
                        case (int)ResponseCodes.NotFound: 
                        default:
                        return MatchmakingResponse.NotFound;
                    }
                }
            }
            catch
            {
                return MatchmakingResponse.ExceptionThrown;
            }
        }
    }
}