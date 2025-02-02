
using System.Net;
using System.Net.Http.Headers;
using System.Text;

public class AdminEndpoint
{
    private HttpListener _listener;
    private string _password;

    public AdminEndpoint(string domain, string password)
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add(domain);
        _password = password;
    }

    public AdminEndpoint(IEnumerable<string> domains, string password)
    {
        _listener = new HttpListener();
        foreach (var domain in domains)
            _listener.Prefixes.Add(domain);
        _password = password;
    }

    public bool IsActive { get; private set; } = false;

    public async void Start()
    {
        _listener.Start();
        IsActive = true;

        File.WriteAllText("logs/admin-endpoint.log", "");

        while (IsActive)
        {
            var context = await _listener.GetContextAsync();
            var request = context.Request;
            var response = context.Response;

            _ = File.AppendAllTextAsync("logs/admin-endpoint.log", $"admin request made by {request.RemoteEndPoint} to {request.LocalEndPoint}{request.Url?.AbsolutePath ?? "/"} at {DateTime.Now}.\n\n");

            response.AddHeader("Access-Control-Allow-Origin", "*");
            response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");
            response.AddHeader("Strict-Transport-Security", "max-age=31536000; includeSubdomains");

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = (int)HttpStatusCode.OK;
                response.Close();
                continue;
            }

            try
            {
                if (Authenticate(request))
                    HandleAdminRequest(request, response);
                else
                {
                    response.StatusCode = (int)AdminResponseCodes.IncorrectCredentials;
                    _ = File.AppendAllTextAsync("logs/admin-endpoint.log", "admin request failed to authenticate.\n\n");
                }
            }
            catch (Exception e)
            {
                response.StatusCode = (int) AdminResponseCodes.RequestRejected;
                _ = File.AppendAllTextAsync("logs/admin-endpoint.log", $"exception thrown at admin endpoint: {e}\n\n");
            }
            response.OutputStream.Close();
        }

        _listener.Close();
    }

    public delegate SessionListResponse SessionListRequestHandler();
    public SessionListRequestHandler OnSessionListRequest;

    public delegate SessionDetailsResponse SessionDetailsRequestHandler(string sessionId);
    public SessionDetailsRequestHandler OnSessionDetailsRequest;

    private void HandleAdminRequest(HttpListenerRequest request, HttpListenerResponse response)
    {
        if (request.Url?.AbsolutePath == "/admin/login")
        {
            var responseObj = new LoginResponse{
                accepted = true
            };
            string responseBody = responseObj.Serialize();
            response.StatusCode = (int)AdminResponseCodes.LoginSuccess;
            byte[] buffer = Encoding.UTF8.GetBytes(responseBody);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);

        }
        else if (request.Url?.AbsolutePath == "/admin/session-list")
        {
            var responseObj = OnSessionListRequest.Invoke();
            string responseBody = responseObj.Serialize();
            response.StatusCode = (int)responseObj.responseCode;
            byte[] buffer = Encoding.UTF8.GetBytes(responseBody);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }
        else if (request.Url?.AbsolutePath.Contains("/admin/sessions/") ?? false)
        {
            var sessionId = request.Url?.AbsolutePath.Split('/').Last();
            var responseObj = OnSessionDetailsRequest.Invoke(sessionId);
            string responseBody = responseObj.Serialize();
            response.StatusCode = (int)responseObj.responseCode;
            byte[] buffer = Encoding.UTF8.GetBytes(responseBody);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }
    }

    private bool Authenticate(HttpListenerRequest request)
    {
        var authHeader = AuthenticationHeaderValue.Parse(request.Headers["Authorization"]);
        var credentialBytes = Convert.FromBase64String(authHeader.Parameter);
        return Encoding.UTF8.GetString(credentialBytes) == _password;
    }

    public void Close()
    {
        IsActive = false;
    }
}