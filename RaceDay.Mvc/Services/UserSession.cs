namespace RaceDay.Mvc.Services
{
    // Everything the website remembers about the logged-in user, kept in the MVC's own
    // server-side session. The browser only holds a session cookie, never these values.
    public class UserSession
    {
        private const string UserIdKey = "UserId";
        private const string RoleKey = "Role";
        private const string FullNameKey = "FullName";
        private const string ApiCookieKey = "ApiCookie";
 
        private readonly IHttpContextAccessor _accessor;
 
        public UserSession(IHttpContextAccessor accessor)
        {
            _accessor = accessor;
        }
 
        private ISession Session => _accessor.HttpContext?.Session
            ?? throw new InvalidOperationException("There is no active web request.");
 
        public bool IsAuthenticated => Session.GetInt32(UserIdKey) != null;
 
        public int? UserId => Session.GetInt32(UserIdKey);
 
        public string? Role => Session.GetString(RoleKey);
 
        public string? FullName => Session.GetString(FullNameKey);
 
        // The API's own session cookie ("name=value"), which we attach to every API call
        // made on this user's behalf. It is the key to their session on the API side.
        public string? ApiCookie => Session.GetString(ApiCookieKey);
 
        public bool IsOrganiser => Role == "Organiser";
 
        public bool IsParticipant => Role == "Participant";
 
        public void SignIn(int userId, string role, string fullName, string apiCookie)
        {
            Session.SetInt32(UserIdKey, userId);
            Session.SetString(RoleKey, role);
            Session.SetString(FullNameKey, fullName);
            Session.SetString(ApiCookieKey, apiCookie);
        }
 
        public void SignOut()
        {
            Session.Clear();
        }
    }
}