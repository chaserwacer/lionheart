using System.Net.Http.Headers;
using System.Text.Json;
using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.Request;
using lionheart.Model.Strava;
using lionheart.Model.User;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services
{
    /// <summary>
    /// Service responsible for the Strava OAuth2 connection and syncing a user's activities into the
    /// lionheart database.
    /// </summary>
    public interface IStravaService
    {
        /// <summary>
        /// Build the Strava authorize URL the browser should be redirected to in order to start the OAuth flow.
        /// </summary>
        Result<string> GetAuthorizeUrl(string state);

        /// <summary>
        /// Complete the OAuth flow: exchange the authorization code for tokens and persist them.
        /// </summary>
        Task<Result> ConnectAsync(IdentityUser user, string code);

        /// <summary>
        /// Pull the user's activities from Strava into the database (incremental after the most recent stored one).
        /// </summary>
        Task<Result<StravaSyncResultDto>> SyncAsync(IdentityUser user);

        /// <summary>
        /// Report whether the user has connected Strava and when activities were last synced.
        /// </summary>
        Task<Result<StravaStatusDto>> GetStatusAsync(IdentityUser user);

        /// <summary>
        /// Retrieve stored Strava activities for a user within a date range.
        /// </summary>
        Task<Result<List<StravaActivityDTO>>> GetActivitiesAsync(IdentityUser user, DateRangeRequest range);
    }

    /// <summary>
    /// Handles fetching, conversion, and storage of Strava data for users. Mirrors the structure of
    /// <see cref="OuraService"/>, but uses the OAuth2 authorization-code flow (with refresh tokens) since
    /// Strava does not offer personal access tokens.
    /// </summary>
    public class StravaService : IStravaService
    {
        private readonly ModelContext _context;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<StravaService> _logger;
        private const string APPLICATION_NAME = "strava";

        private const string AuthorizeUrl = "https://www.strava.com/oauth/authorize";
        private const string TokenUrl = "https://www.strava.com/oauth/token";
        private const string ActivitiesUrl = "https://www.strava.com/api/v3/athlete/activities";
        private const string Scope = "activity:read_all";

        public StravaService(ModelContext context, HttpClient httpClient, IConfiguration configuration, ILogger<StravaService> logger)
        {
            _context = context;
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        private string? ClientId => _configuration["Strava:ClientId"];
        private string? ClientSecret => _configuration["Strava:ClientSecret"];
        private string? RedirectUri => _configuration["Strava:RedirectUri"];

        public Result<string> GetAuthorizeUrl(string state)
        {
            if (string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(RedirectUri))
            {
                return Result<string>.Error("Strava integration is not configured on the server.");
            }

            var url = $"{AuthorizeUrl}?client_id={Uri.EscapeDataString(ClientId)}" +
                      $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                      "&response_type=code" +
                      "&approval_prompt=auto" +
                      $"&scope={Uri.EscapeDataString(Scope)}" +
                      $"&state={Uri.EscapeDataString(state)}";

            return Result<string>.Success(url);
        }

        public async Task<Result> ConnectAsync(IdentityUser user, string code)
        {
            if (string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(ClientSecret))
            {
                return Result.Error("Strava integration is not configured on the server.");
            }

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = ClientId!,
                ["client_secret"] = ClientSecret!,
                ["code"] = code,
                ["grant_type"] = "authorization_code",
            });

            var response = await _httpClient.PostAsync(TokenUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Strava token exchange failed ({Status}): {Body}", response.StatusCode, body);
                return Result.Error("Failed to connect to Strava. Please try again.");
            }

            var json = await response.Content.ReadAsStringAsync();
            var token = JsonSerializer.Deserialize<StravaTokenResponse>(json);
            if (token is null || string.IsNullOrEmpty(token.AccessToken))
            {
                return Result.Error("Received an invalid response from Strava.");
            }

            await UpsertTokenAsync(Guid.Parse(user.Id), token);
            return Result.Success();
        }

        public async Task<Result<StravaSyncResultDto>> SyncAsync(IdentityUser user)
        {
            var userGuid = Guid.Parse(user.Id);
            var tokenRow = await _context.ApiAccessTokens
                .FirstOrDefaultAsync(a => a.UserID == userGuid && a.ApplicationName == APPLICATION_NAME);

            if (tokenRow is null)
            {
                return Result<StravaSyncResultDto>.Success(new StravaSyncResultDto(Connected: false, ReauthRequired: false, Imported: 0, Updated: 0));
            }

            var accessToken = await EnsureValidAccessTokenAsync(tokenRow);
            if (accessToken is null)
            {
                return Result<StravaSyncResultDto>.Success(new StravaSyncResultDto(Connected: true, ReauthRequired: true, Imported: 0, Updated: 0));
            }

            // Incremental: only fetch activities newer than the most recent one we already stored.
            var latestStartDate = await _context.StravaActivities
                .Where(a => a.UserID == userGuid)
                .OrderByDescending(a => a.StartDate)
                .Select(a => (DateTime?)a.StartDate)
                .FirstOrDefaultAsync();

            long after = latestStartDate.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(latestStartDate.Value, DateTimeKind.Utc)).ToUnixTimeSeconds()
                : 0;

            int imported = 0;
            int updated = 0;
            int page = 1;
            const int perPage = 100;
            var now = DateTime.UtcNow;

            while (true)
            {
                var url = $"{ActivitiesUrl}?after={after}&per_page={perPage}&page={page}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    // Token rejected mid-sync (e.g. revoked); ask the user to reconnect.
                    return Result<StravaSyncResultDto>.Success(new StravaSyncResultDto(Connected: true, ReauthRequired: true, Imported: imported, Updated: updated));
                }
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Strava activities fetch failed ({Status}): {Body}", response.StatusCode, body);
                    return Result<StravaSyncResultDto>.Error("Failed to fetch activities from Strava.");
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var elements = doc.RootElement.EnumerateArray().ToList();
                if (elements.Count == 0)
                {
                    break;
                }

                // Deserialize the page, then batch-load any already-stored rows in a single query
                // (avoids one existence query per activity).
                var summaries = elements
                    .Select(e => (RawJson: e.GetRawText(), Summary: JsonSerializer.Deserialize<StravaSummaryActivity>(e.GetRawText())))
                    .Where(x => x.Summary is not null && x.Summary.Id != 0)
                    .ToList();

                var pageIds = summaries.Select(x => x.Summary!.Id).ToList();
                var existingById = await _context.StravaActivities
                    .Where(a => a.UserID == userGuid && pageIds.Contains(a.StravaActivityID))
                    .ToDictionaryAsync(a => a.StravaActivityID);

                foreach (var (rawJson, summary) in summaries)
                {
                    if (existingById.TryGetValue(summary!.Id, out var existing))
                    {
                        ApplySummary(existing, summary, rawJson, now);
                        updated++;
                    }
                    else
                    {
                        _context.StravaActivities.Add(MapToEntity(userGuid, summary, rawJson, now, Guid.NewGuid()));
                        imported++;
                    }
                }

                await _context.SaveChangesAsync();

                if (elements.Count < perPage)
                {
                    break;
                }
                page++;
            }

            return Result<StravaSyncResultDto>.Success(new StravaSyncResultDto(Connected: true, ReauthRequired: false, Imported: imported, Updated: updated));
        }

        public async Task<Result<StravaStatusDto>> GetStatusAsync(IdentityUser user)
        {
            var userGuid = Guid.Parse(user.Id);
            var connected = await _context.ApiAccessTokens
                .AnyAsync(a => a.UserID == userGuid && a.ApplicationName == APPLICATION_NAME);

            DateTime? lastSyncedAt = null;
            if (connected)
            {
                lastSyncedAt = await _context.StravaActivities
                    .Where(a => a.UserID == userGuid)
                    .OrderByDescending(a => a.SyncedAt)
                    .Select(a => (DateTime?)a.SyncedAt)
                    .FirstOrDefaultAsync();
            }

            return Result<StravaStatusDto>.Success(new StravaStatusDto(connected, lastSyncedAt));
        }

        public async Task<Result<List<StravaActivityDTO>>> GetActivitiesAsync(IdentityUser user, DateRangeRequest range)
        {
            var userGuid = Guid.Parse(user.Id);
            var start = range.StartDate;
            // EndDate binds to midnight for a date-only value; use an exclusive upper bound at the
            // start of the next day so activities later on the end date are included.
            var endExclusive = range.EndDate.Date.AddDays(1);

            var activities = await _context.StravaActivities
                .AsNoTracking()
                .Where(a => a.UserID == userGuid && a.StartDate >= start && a.StartDate < endExclusive)
                .OrderByDescending(a => a.StartDate)
                .ToListAsync();

            return Result<List<StravaActivityDTO>>.Success(activities.Select(a => a.ToDTO()).ToList());
        }

        /// <summary>
        /// Returns a usable access token, refreshing it first if it has expired. Returns null if the token
        /// could not be refreshed (the user must reconnect).
        /// </summary>
        private async Task<string?> EnsureValidAccessTokenAsync(ApiAccessToken tokenRow)
        {
            // 60s of leeway so we don't start a long sync with a token about to expire.
            if (tokenRow.ExpiresAt.HasValue && tokenRow.ExpiresAt.Value > DateTime.UtcNow.AddSeconds(60))
            {
                return tokenRow.PersonalAccessToken;
            }

            if (string.IsNullOrWhiteSpace(tokenRow.RefreshToken) || string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(ClientSecret))
            {
                return null;
            }

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = ClientId!,
                ["client_secret"] = ClientSecret!,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = tokenRow.RefreshToken!,
            });

            var response = await _httpClient.PostAsync(TokenUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Strava token refresh failed ({Status}): {Body}", response.StatusCode, body);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var token = JsonSerializer.Deserialize<StravaTokenResponse>(json);
            if (token is null || string.IsNullOrEmpty(token.AccessToken))
            {
                return null;
            }

            tokenRow.PersonalAccessToken = token.AccessToken;
            tokenRow.RefreshToken = token.RefreshToken;
            tokenRow.ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(token.ExpiresAt).UtcDateTime;
            await _context.SaveChangesAsync();

            return token.AccessToken;
        }

        private async Task UpsertTokenAsync(Guid userGuid, StravaTokenResponse token)
        {
            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(token.ExpiresAt).UtcDateTime;
            var existing = await _context.ApiAccessTokens
                .FirstOrDefaultAsync(a => a.UserID == userGuid && a.ApplicationName == APPLICATION_NAME);

            if (existing is not null)
            {
                existing.PersonalAccessToken = token.AccessToken;
                existing.RefreshToken = token.RefreshToken;
                existing.ExpiresAt = expiresAt;
            }
            else
            {
                _context.ApiAccessTokens.Add(new ApiAccessToken
                {
                    ObjectID = Guid.NewGuid(),
                    UserID = userGuid,
                    ApplicationName = APPLICATION_NAME,
                    PersonalAccessToken = token.AccessToken,
                    RefreshToken = token.RefreshToken,
                    ExpiresAt = expiresAt,
                });
            }

            await _context.SaveChangesAsync();
        }

        private static StravaActivity MapToEntity(Guid userGuid, StravaSummaryActivity summary, string rawJson, DateTime syncedAt, Guid objectId)
        {
            var entity = new StravaActivity
            {
                ObjectID = objectId,
                UserID = userGuid,
            };
            ApplySummary(entity, summary, rawJson, syncedAt);
            return entity;
        }

        private static void ApplySummary(StravaActivity entity, StravaSummaryActivity summary, string rawJson, DateTime syncedAt)
        {
            entity.StravaActivityID = summary.Id;
            entity.Name = summary.Name;
            entity.SportType = summary.SportType ?? summary.Type ?? string.Empty;
            entity.StartDate = DateTime.SpecifyKind(summary.StartDate, DateTimeKind.Utc);
            // start_date_local is wall-clock local time despite Strava's misleading "Z" suffix.
            entity.StartDateLocal = DateTime.SpecifyKind(summary.StartDateLocal, DateTimeKind.Unspecified);
            entity.ElapsedTimeSeconds = summary.ElapsedTime;
            entity.MovingTimeSeconds = summary.MovingTime;
            entity.DistanceMeters = summary.Distance;
            entity.TotalElevationGainMeters = summary.TotalElevationGain;
            entity.AverageSpeed = summary.AverageSpeed;
            entity.MaxSpeed = summary.MaxSpeed;
            entity.AverageHeartrate = summary.AverageHeartrate;
            entity.MaxHeartrate = summary.MaxHeartrate;
            entity.RawJson = rawJson;
            entity.SyncedAt = syncedAt;
        }
    }
}
