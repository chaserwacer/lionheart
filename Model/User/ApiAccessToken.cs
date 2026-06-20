namespace lionheart.Model.User
{
    /// <summary>
    /// Class for holding an API Access token. It contains a private key (ObjectID), as well as a UserID key to associate it with a user. 
    /// It then contains the name of the application and the token for that application. 
    /// </summary>
    public class ApiAccessToken{
        public Guid ObjectID { get; set; }
        public Guid UserID {get; init;}
        public string ApplicationName { get; set; } = string.Empty;
        public string PersonalAccessToken { get; set; } = string.Empty;

        /// <summary>
        /// OAuth2 refresh token. Null for services that use a simple personal access token (e.g. Oura).
        /// Used by services such as Strava to refresh an expired <see cref="PersonalAccessToken"/>.
        /// </summary>
        public string? RefreshToken { get; set; }

        /// <summary>
        /// UTC expiry of the current <see cref="PersonalAccessToken"/> for OAuth2 services. Null when the token does not expire.
        /// </summary>
        public DateTime? ExpiresAt { get; set; }
    }
}