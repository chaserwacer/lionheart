using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace lionheart.Model.Strava
{
    /// <summary>
    /// Response containing the Strava OAuth authorize URL the frontend should redirect the browser to.
    /// </summary>
    public record StravaAuthUrlResponse(string Url);

    /// <summary>
    /// Request body for completing the OAuth flow: the authorization code returned by Strava plus the
    /// anti-CSRF state value that was issued when the flow started.
    /// </summary>
    public record ConnectStravaRequest(
        [Required] string Code,
        [Required] string State
    );

    /// <summary>
    /// Result of a manual sync. <see cref="ReauthRequired"/> tells the frontend to open the reconnect modal.
    /// </summary>
    public record StravaSyncResultDto(
        bool Connected,
        bool ReauthRequired,
        int Imported,
        int Updated
    );

    /// <summary>
    /// Connection status for the Strava integration.
    /// </summary>
    public record StravaStatusDto(
        bool Connected,
        DateTime? LastSyncedAt
    );

    /// <summary>
    /// Strava's OAuth token response (used for both authorization-code exchange and refresh).
    /// </summary>
    public class StravaTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;

        /// <summary>
        /// Unix epoch (seconds) at which the access token expires.
        /// </summary>
        [JsonPropertyName("expires_at")]
        public long ExpiresAt { get; set; }
    }

    /// <summary>
    /// Subset of Strava's <c>SummaryActivity</c> used when deserializing the athlete activities list.
    /// </summary>
    public class StravaSummaryActivity
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("sport_type")]
        public string? SportType { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("start_date")]
        public DateTime StartDate { get; set; }

        [JsonPropertyName("start_date_local")]
        public DateTime StartDateLocal { get; set; }

        [JsonPropertyName("elapsed_time")]
        public int ElapsedTime { get; set; }

        [JsonPropertyName("moving_time")]
        public int MovingTime { get; set; }

        [JsonPropertyName("distance")]
        public double Distance { get; set; }

        [JsonPropertyName("total_elevation_gain")]
        public double TotalElevationGain { get; set; }

        [JsonPropertyName("average_speed")]
        public double AverageSpeed { get; set; }

        [JsonPropertyName("max_speed")]
        public double MaxSpeed { get; set; }

        [JsonPropertyName("average_heartrate")]
        public double? AverageHeartrate { get; set; }

        [JsonPropertyName("max_heartrate")]
        public double? MaxHeartrate { get; set; }
    }
}
