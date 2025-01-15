
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OwlTree.Matchmaking
{
    /// <summary>
    /// Matchmaking HTTP response codes.
    /// </summary>
    public enum ResponseCodes
    {
        /// <summary>
        /// An invalid response code, this should not be returned.
        /// </summary>
        Invalid = 0,

        /// <summary>
        /// The matchmaking request was accepted.
        /// </summary>
        RequestAccepted = 200,

        /// <summary>
        /// The endpoint or URI was not found.
        /// </summary>
        NotFound = 404,
        /// <summary>
        /// The request failed to send.
        /// </summary>
        ExceptionThrow = 410,
        /// <summary>
        /// The endpoint rejected the matchmaking request.
        /// </summary>
        RequestRejected = 411
    }

    /// <summary>
    /// Sent by the matchmaking endpoint in response to a matchmaking request from a client.
    /// This will contain data needed to make an OwlTree Connection.
    /// </summary>
    public struct MatchmakingResponse
    {
        /// <summary>
        /// The HTTP response code.
        /// </summary>
        public ResponseCodes responseCode { get; set; }

        /// <summary>
        /// Returns true if the response has a successful response code.
        /// </summary>
        [JsonIgnore]
        public bool RequestSuccessful => 200 <= (int)responseCode && (int)responseCode <= 299;
        /// <summary>
        /// Returns true if the response has a failure response code.
        /// </summary>
        [JsonIgnore]
        public bool RequestFailed => 400 <= (int)responseCode && (int)responseCode <= 499;

        /// <summary>
        /// The IP address of the server or relay connection.
        /// </summary>
        public string serverAddr { get; set; }

        /// <summary>
        /// The UDP port of the server or relay connection.
        /// </summary>
        public int udpPort { get; set; }

        /// <summary>
        /// The TCP port of the server or relay connection.
        /// </summary>
        public int tcpPort { get; set; }

        /// <summary>
        /// The session id of the server or relay connection.
        /// </summary>
        public string sessionId { get; set; }
        
        /// <summary>
        /// The session id of the server or relay connection.
        /// </summary>
        public string appId { get; set; }

        /// <summary>
        /// Whether the created session is server authoritative, or relayed peer-to-peer.
        /// </summary>
        public ServerType serverType { get; set; }

        /// <summary>
        /// Serializes the response to a JSON string.
        /// </summary>
        public string Serialize()
        {
            return JsonSerializer.Serialize(this);
        }

        /// <summary>
        /// Deserializes a response from a JSON string.
        /// </summary>
        public static MatchmakingResponse Deserialize(string data)
        {
            return JsonSerializer.Deserialize<MatchmakingResponse>(data);
        }

        /// <summary>
        /// Response for a not found failure.
        /// </summary>
        public static MatchmakingResponse NotFound = new MatchmakingResponse{
            responseCode = ResponseCodes.NotFound
        };

        /// <summary>
        /// Response for an exception thrown failure.
        /// </summary>
        public static MatchmakingResponse ExceptionThrown = new MatchmakingResponse{
            responseCode = ResponseCodes.ExceptionThrow,
        };

        /// <summary>
        /// Response for a rejection failure.
        /// </summary>
        public static MatchmakingResponse RequestRejected = new MatchmakingResponse{
            responseCode = ResponseCodes.RequestRejected
        };
    }
}