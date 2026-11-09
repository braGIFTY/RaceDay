using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RaceDay.Mvc.Models;
 
namespace RaceDay.Mvc.Services
{
    // Marker for calls that return no body (for example a 204 No Content)
    public sealed class NoContent { }
 
    public class ApiResult
    {
        public bool IsSuccess { get; init; }
 
        public HttpStatusCode StatusCode { get; init; }
 
        // A message that is safe to show to a user. Never raw exception text.
        public string? Error { get; init; }
 
        // Validation problems by field name, for showing next to form fields
        public Dictionary<string, string[]> FieldErrors { get; init; } = new();
 
        public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;
 
        public bool IsForbidden => StatusCode == HttpStatusCode.Forbidden;
 
        public bool IsNotFound => StatusCode == HttpStatusCode.NotFound;
    }
 
    public class ApiResult<T> : ApiResult
    {
        public T? Data { get; init; }
    }
 
    // The only class in the website that talks to the API. Controllers never build their own
    // HTTP requests, so session handling and error handling live in exactly one place.
    public class ApiClient
    {
        private const string ApiSessionCookieName = ".AspNetCore.Session";
        private const string UnreachableMessage = "We couldn't reach the RaceDay service. Please try again in a moment.";
 
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
 
        private readonly HttpClient _http;
        private readonly UserSession _session;
        private readonly ILogger<ApiClient> _logger;
 
        public ApiClient(HttpClient http, UserSession session, ILogger<ApiClient> logger)
        {
            _http = http;
            _session = session;
            _logger = logger;
        }
 
        // ---------- Account ----------
 
        public Task<ApiResult<AuthResponseDto>> RegisterAsync(
            string role, string fullName, string email, string password, CancellationToken cancellationToken = default)
        {
            var path = role switch
            {
                "Organiser" => "api/auth/register/organiser",
                "Participant" => "api/auth/register/participant",
                _ => null
            };
 
            if (path == null)
                return Task.FromResult(Failure<AuthResponseDto>(HttpStatusCode.BadRequest, "Please choose a role."));
 
            return PostAsync<AuthResponseDto>(path, new { fullName, email, password }, cancellationToken);
        }
 
        // Logs in through the API, then remembers the user (and the API's session cookie) in our own session
        public async Task<ApiResult<AuthResponseDto>> LoginAsync(
            string email, string password, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/login")
            {
                Content = JsonContent.Create(new { email, password }, options: JsonOptions)
            };
 
            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Could not reach the API to log in");
                return Failure<AuthResponseDto>(HttpStatusCode.ServiceUnavailable, UnreachableMessage);
            }
 
            using (response)
            {
                if (!response.IsSuccessStatusCode)
                    return await FailureFromResponseAsync<AuthResponseDto>(response, cancellationToken);
 
                var user = await ReadBodyAsync<AuthResponseDto>(response, cancellationToken);
                var cookie = ExtractSessionCookie(response);
 
                if (user == null || cookie == null)
                    return Failure<AuthResponseDto>(HttpStatusCode.BadGateway, "Logging in didn't work. Please try again.");
 
                _session.SignIn(user.Id, user.Role, user.FullName, cookie);
 
                return new ApiResult<AuthResponseDto> { IsSuccess = true, StatusCode = response.StatusCode, Data = user };
            }
        }
 
        public async Task LogoutAsync(CancellationToken cancellationToken = default)
        {
            // End the session on the API side too, not just ours
            if (_session.IsAuthenticated)
                await SendAsync<NoContent>(HttpMethod.Post, "api/auth/logout", null, cancellationToken);
 
            // Always end our own session, even if the API couldn't be reached
            _session.SignOut();
        }
 
        // ---------- General calls, used by every page ----------
 
        public Task<ApiResult<T>> GetAsync<T>(string path, CancellationToken cancellationToken = default) =>
            SendAsync<T>(HttpMethod.Get, path, null, cancellationToken);
 
        public Task<ApiResult<T>> PostAsync<T>(string path, object? body = null, CancellationToken cancellationToken = default) =>
            SendAsync<T>(HttpMethod.Post, path, ToJson(body), cancellationToken);
 
        public Task<ApiResult<T>> PutAsync<T>(string path, object body, CancellationToken cancellationToken = default) =>
            SendAsync<T>(HttpMethod.Put, path, ToJson(body), cancellationToken);
 
        public Task<ApiResult<NoContent>> DeleteAsync(string path, CancellationToken cancellationToken = default) =>
            SendAsync<NoContent>(HttpMethod.Delete, path, null, cancellationToken);
 
        // Sends an uploaded file on to the API (the website never talks to Azure itself)

        // dis waan
        public Task<ApiResult<T>> PostFileAsync<T>(string path, IFormFile? file, CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length == 0)
                return Task.FromResult(Failure<T>(HttpStatusCode.BadRequest, "Please choose an image to upload."));
 
            var form = new MultipartFormDataContent();
            var fileContent = new StreamContent(file.OpenReadStream());
 
            if (MediaTypeHeaderValue.TryParse(file.ContentType, out var mediaType))
                fileContent.Headers.ContentType = mediaType;
 
            form.Add(fileContent, "file", Path.GetFileName(file.FileName));
 
            return SendAsync<T>(HttpMethod.Post, path, form, cancellationToken);
        }
 
        // ---------- Plumbing ----------
 
        private static JsonContent? ToJson(object? body) =>
            body == null ? null : JsonContent.Create(body, body.GetType(), options: JsonOptions);
 
        private async Task<ApiResult<T>> SendAsync<T>(
            HttpMethod method, string path, HttpContent? content, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(method, path) { Content = content };
 
            // Act as this user: send their own API session cookie, and nobody else's
            if (_session.IsAuthenticated && !string.IsNullOrEmpty(_session.ApiCookie))
                request.Headers.Add("Cookie", _session.ApiCookie);
 
            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Could not reach the API at {Path}", path);
                return Failure<T>(HttpStatusCode.ServiceUnavailable, UnreachableMessage);
            }
 
            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    // The API no longer recognises this user's session (timed out, or the API restarted),
                    // so ours is useless too. Clearing it makes the site send them back to the login page.
                    if (response.StatusCode == HttpStatusCode.Unauthorized && _session.IsAuthenticated)
                        _session.SignOut();
 
                    return await FailureFromResponseAsync<T>(response, cancellationToken);
                }
 
                T? data = default;
                if (typeof(T) != typeof(NoContent) && response.StatusCode != HttpStatusCode.NoContent)
                    data = await ReadBodyAsync<T>(response, cancellationToken);
 
                return new ApiResult<T> { IsSuccess = true, StatusCode = response.StatusCode, Data = data };
            }
        }
 
        private static async Task<T?> ReadBodyAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            try
            {
                return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            }
            catch (JsonException)
            {
                return default;
            }
        }
 
        // The API's session cookie arrives as "name=value; path=/; httponly": we only keep "name=value"
        private static string? ExtractSessionCookie(HttpResponseMessage response)
        {
            if (!response.Headers.TryGetValues("Set-Cookie", out var headers))
                return null;
 
            foreach (var header in headers)
            {
                var pair = header.Split(';', 2)[0].Trim();
                if (pair.StartsWith(ApiSessionCookieName + "=", StringComparison.Ordinal))
                    return pair;
            }
 
            return null;
        }
 
        private static ApiResult<T> Failure<T>(HttpStatusCode status, string message) =>
            new() { IsSuccess = false, StatusCode = status, Error = message };
 
        // Turns whatever the API said into a message that is safe and kind to show on screen
        private static async Task<ApiResult<T>> FailureFromResponseAsync<T>(
            HttpResponseMessage response, CancellationToken cancellationToken)
        {
            string? message = null;
            var fieldErrors = new Dictionary<string, string[]>();
 
            try
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(body))
                {
                    using var document = JsonDocument.Parse(body);
                    var root = document.RootElement;
 
                    if (root.ValueKind == JsonValueKind.Object)
                    {
                        // The API's own plain messages, such as "Email already registered."
                        if (root.TryGetProperty("message", out var apiMessage) && apiMessage.ValueKind == JsonValueKind.String)
                            message = apiMessage.GetString();
 
                        // ASP.NET validation failures: { "errors": { "Password": ["..."] } }
                        if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var field in errors.EnumerateObject())
                            {
                                if (field.Value.ValueKind != JsonValueKind.Array)
                                    continue;
 
                                var messages = field.Value.EnumerateArray()
                                    .Where(item => item.ValueKind == JsonValueKind.String)
                                    .Select(item => item.GetString()!)
                                    .ToArray();
 
                                if (messages.Length > 0)
                                    fieldErrors[field.Name] = messages;
                            }
 
                            message ??= fieldErrors.Values.SelectMany(m => m).FirstOrDefault();
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Not JSON (for example an HTML error page): fall back to a generic message below
            }
 
            message ??= FriendlyMessage(response.StatusCode);
 
            return new ApiResult<T>
            {
                IsSuccess = false,
                StatusCode = response.StatusCode,
                Error = message,
                FieldErrors = fieldErrors
            };
        }
 
        private static string FriendlyMessage(HttpStatusCode status) => status switch
        {
            HttpStatusCode.BadRequest => "Some of the details weren't valid. Please check them and try again.",
            HttpStatusCode.Unauthorized => "Your session has ended. Please log in again.",
            HttpStatusCode.Forbidden => "You don't have permission to do that.",
            HttpStatusCode.NotFound => "We couldn't find what you were looking for.",
            HttpStatusCode.Conflict => "That conflicts with something that already exists.",
            HttpStatusCode.ServiceUnavailable => "That isn't available right now. Please try again shortly.",
            _ when (int)status >= 500 => "Something went wrong on our side. Please try again.",
            _ => "That didn't work. Please try again."
        };
    }
}
 
