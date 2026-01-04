using System.Net.Http.Headers;
using lionheart.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using lionheart.Model.Oura.Dto;
using lionheart.Model.Oura;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using lionheart.Model.DTOs;
using Ardalis.Result;

namespace lionheart.Services
{
    public interface IOuraService
    {
        /// <summary>
        /// Synchronize Oura data for a user within a specified date range.
        /// </summary>
        Task<Result> SyncOuraAPI(IdentityUser user, DateRangeRequest dateRange);
        /// <summary>
        /// Get the daily Oura information for a user on a specific date.
        /// </summary>
        Task<Result<DailyOuraDataDTO>> GetDailyOuraInfoAsync(IdentityUser user, DateOnly date);
        /// <summary>
        /// Get the daily Oura information for a user within a specified date range.
        /// </summary>
        Task<Result<List<DailyOuraDataDTO>>> GetDailyOuraInfosAsync(IdentityUser user, DateRangeRequest dateRange);
    }
    /// <summary>
    /// OuraService handles fetching, conversion, and storage of Oura Ring data for users.
    /// </summary>
    /// <remarks>
    /// This service interacts with the Oura Ring API.
    /// Data is fetched and deserialzed into objects mirroring structure defined in their Open API documentation.
    /// Those objects are then converted into custom representation <see cref="DailyOuraData"/> objects for storage in the database.
    /// </remarks>
    public class OuraService : IOuraService
    {
        private readonly ModelContext _context;
        private readonly HttpClient _httpClient;
        private readonly string APPLICATION_NAME = "oura";
        //private const string BaseUrl = "https://api.ouraring.com/v2/usercollection/";

        public OuraService(ModelContext context, HttpClient httpClient)
        {
            _httpClient = httpClient;
            _context = context;
        }

        public async Task<Result<DailyOuraDataDTO>> GetDailyOuraInfoAsync(IdentityUser user, DateOnly date)
        {
            var userGuid = Guid.Parse(user.Id);
            var dto = await _context.DailyOuraDatas.FirstOrDefaultAsync(x => x.UserID == userGuid && x.Date == date) ?? null;


            if (dto is not null)
            {
                return Result<DailyOuraDataDTO>.Success(new DailyOuraDataDTO()
                {
                    ObjectID = dto.ObjectID,
                    Date = dto.Date,
                    ResilienceData = dto.ResilienceData,
                    ReadinessData = dto.ReadinessData,
                    SleepData = dto.SleepData,
                    ActivityData = dto.ActivityData,
                });
            }
            else
            {
                return Result<DailyOuraDataDTO>.Success(new DailyOuraDataDTO()
                {
                    ObjectID = new Guid(),
                    Date = date,

                    ResilienceData = new ResilienceData
                    {
                        SleepRecovery = 1.0,
                        DaytimeRecovery = 1.0,
                        Stress = 1.0,
                        ResilienceLevel = "unkown"
                    },
                    ActivityData = new ActivityData
                    {
                        ActivityScore = 0,
                        Steps = 0,
                        ActiveCalories = 0,
                        TotalCalories = 0,
                        TargetCalories = 0,
                        MeetDailyTargets = 1,
                        MoveEveryHour = 1,
                        RecoveryTime = 1,
                        StayActive = 1,
                        TrainingFrequency = 1,
                        TrainingVolume = 1
                    },
                    SleepData = new SleepData
                    {
                        SleepScore = 0,
                        DeepSleep = 1,
                        Efficiency = 1,
                        Latency = 1,
                        RemSleep = 1,
                        Restfulness = 1,
                        Timing = 1,
                        TotalSleep = 1
                    },
                    ReadinessData = new ReadinessData
                    {
                        ReadinessScore = 0,
                        TemperatureDeviation = 0.0,
                        ActivityBalance = 1,
                        BodyTemperature = 1,
                        HrvBalance = 1,
                        PreviousDayActivity = 1,
                        PreviousNight = 1,
                        RecoveryIndex = 1,
                        RestingHeartRate = 1,
                        SleepBalance = 1
                    },
                });
            }
        }

        public async Task<Result> SyncOuraAPI(IdentityUser user, DateRangeRequest dateRange)
        {
            var userGuid = Guid.Parse(user.Id);
            var userPersonalToken = await GetUserPersonalTokenAsync(userGuid);


            // Delete Section
            // var objectsToDelete = _context.DailyOuraInfos.ToList();
            // _context.DailyOuraInfos.RemoveRange(objectsToDelete);
            // _context.SaveChanges();





            // Get Dto objects representing the ouraInfos from the past 'daysPrior'
            var startDate = dateRange.StartDate;
            var endDate = dateRange.EndDate;
            var OuraStateInfoObjects = await _context.DailyOuraDatas.Where(o => o.Date >= startDate && o.Date <= endDate && o.UserID == userGuid).
            Select(o => new DailyOuraInfoDto(
                o.ObjectID,
                o.Date,
                o.SyncDate
            )).ToListAsync();

            OuraStateInfoObjects = [.. OuraStateInfoObjects.OrderBy(o => o.Date)];

            // Determine how far back I need to go to pull oura data
            DateOnly earliestDateToInclude = startDate;
            for (int i = 0; i < OuraStateInfoObjects.Count; i++)
            {
                if (OuraStateInfoObjects[i].SyncDate > OuraStateInfoObjects[i].Date)
                {
                    earliestDateToInclude = OuraStateInfoObjects[i].Date.AddDays(1);
                }
            }

            // Call Oura APi's to recieve stuctured data
            /*
            KNOWN ISSUE: The activity api doesnt wori the same as the other three. For some reason, you must have the end date be one day after the actual end date you want to include.
            This appears to be a bug in the Oura API.
            */
            var activityUrl = "https://api.ouraring.com/v2/usercollection/daily_activity?start_date=" + earliestDateToInclude.ToString("yyyy-MM-dd") + "&end_date=" + endDate.AddDays(1).ToString("yyyy-MM-dd");
            var resilienceUrl = "https://api.ouraring.com/v2/usercollection/daily_resilience?start_date=" + earliestDateToInclude.ToString("yyyy-MM-dd") + "&end_date=" + endDate.ToString("yyyy-MM-dd");
            var sleepUrl = "https://api.ouraring.com/v2/usercollection/daily_sleep?start_date=" + earliestDateToInclude.ToString("yyyy-MM-dd") + "&end_date=" + endDate.ToString("yyyy-MM-dd");
            var readinessUrl = "https://api.ouraring.com/v2/usercollection/daily_readiness?start_date=" + earliestDateToInclude.ToString("yyyy-MM-dd") + "&end_date=" + endDate.ToString("yyyy-MM-dd");


            //TODO: Update to return an uncessfessful result 
            if (earliestDateToInclude > endDate)
            {
                return Result.Error("Earliest date to include is after the end date. No data to sync.");
            }

            (List<OuraDailyActivityDocument> activityDocuments, string activityJson) = await GetOuraDataFromApiAsync<OuraDailyActivityDocument>(activityUrl, userPersonalToken);
            (List<OuraDailyResilienceDocument> resilienceDocuments, string resilienceJson) = await GetOuraDataFromApiAsync<OuraDailyResilienceDocument>(resilienceUrl, userPersonalToken);
            (List<OuraDailySleepDocument> sleepDocuments, string sleepJson) = await GetOuraDataFromApiAsync<OuraDailySleepDocument>(sleepUrl, userPersonalToken);
            (List<OuraDailyReadinessDocument> readinessDocuments, string readinessJson) = await GetOuraDataFromApiAsync<OuraDailyReadinessDocument>(readinessUrl, userPersonalToken);

            var numberDays = endDate.DayNumber - earliestDateToInclude.DayNumber + 1;
            ActivityData activityData;
            ResilienceData resilienceData;
            SleepData sleepData;
            ReadinessData readinessData;
            var currentDate = earliestDateToInclude;

            while (currentDate <= endDate)
            {


                // Build pieces of Daily Oura object via creating its subobjects, who contain pieces of each of the different documents I acquired from Oura.
                var activityDocument = activityDocuments.FirstOrDefault(d => d.Day == currentDate);
                if (activityDocument != null)
                {
                    activityData = new()
                    {
                        ActivityScore = activityDocument.Score ?? 0,
                        Steps = activityDocument.Steps,
                        ActiveCalories = activityDocument.ActiveCalories,
                        TotalCalories = activityDocument.TotalCalories,
                        TargetCalories = activityDocument.TargetCalories,
                        MeetDailyTargets = activityDocument.Contributors.MeetDailyTargets ?? 0,
                        MoveEveryHour = activityDocument.Contributors.MoveEveryHour ?? 0,
                        RecoveryTime = activityDocument.Contributors.RecoveryTime ?? 0,
                        StayActive = activityDocument.Contributors.StayActive ?? 0,
                        TrainingFrequency = activityDocument.Contributors.TrainingFrequency ?? 0,
                        TrainingVolume = activityDocument.Contributors.TrainingVolume ?? 0,
                    };
                }
                else
                {
                    activityData = new()
                    {
                        ActivityScore = -1,
                        Steps = 0,
                        ActiveCalories = 0,
                        TotalCalories = 0,
                        TargetCalories = 0,
                        MeetDailyTargets = 0,
                        MoveEveryHour = 0,
                        RecoveryTime = 0,
                        StayActive = 0,
                        TrainingFrequency = 0,
                        TrainingVolume = 0,
                    };
                }

                var resilienceDocument = resilienceDocuments.FirstOrDefault(r => r.Day == currentDate);

                if (resilienceDocument != null)
                {
                    resilienceData = new()
                    {
                        SleepRecovery = resilienceDocument.Contributors.SleepRecovery,
                        DaytimeRecovery = resilienceDocument.Contributors.DaytimeRecovery,
                        Stress = resilienceDocument.Contributors.Stress,
                        ResilienceLevel = resilienceDocument.Level,
                    };
                }
                else
                {
                    resilienceData = new()
                    {
                        SleepRecovery = -1,
                        DaytimeRecovery = -1,
                        Stress = -1,
                        ResilienceLevel = "unknown",
                    };
                }

                var sleepDocument = sleepDocuments.FirstOrDefault(s => s.Day == currentDate);
                if (sleepDocument != null)
                {
                    sleepData = new()
                    {
                        SleepScore = sleepDocument.Score ?? 0,
                        DeepSleep = sleepDocument.Contributors.DeepSleep ?? 0,
                        Efficiency = sleepDocument.Contributors.Efficiency ?? 0,
                        Latency = sleepDocument.Contributors.Latency ?? 0,
                        RemSleep = sleepDocument.Contributors.RemSleep ?? 0,
                        Restfulness = sleepDocument.Contributors.Restfulness ?? 0,
                        Timing = sleepDocument.Contributors.Timing ?? 0,
                        TotalSleep = sleepDocument.Contributors.TotalSleep ?? 0,
                    };
                }
                else
                {
                    sleepData = new()
                    {
                        SleepScore = -1,
                        DeepSleep = 0,
                        Efficiency = 0,
                        Latency = 0,
                        RemSleep = 0,
                        Restfulness = 0,
                        Timing = 0,
                        TotalSleep = 0,
                    };
                }

                var readinessDocument = readinessDocuments.FirstOrDefault(r => r.Day == currentDate);
                if (readinessDocument != null)
                {
                    readinessData = new()
                    {
                        ReadinessScore = readinessDocument.Score ?? 0,
                        TemperatureDeviation = readinessDocument.TemperatureDeviation ?? 0,
                        ActivityBalance = readinessDocument.Contributors.ActivityBalance ?? 0,
                        BodyTemperature = readinessDocument.Contributors.BodyTemperature ?? 0,
                        HrvBalance = readinessDocument.Contributors.HrvBalance ?? 0,
                        PreviousDayActivity = readinessDocument.Contributors.PreviousDayActivity ?? 0,
                        PreviousNight = readinessDocument.Contributors.PreviousNight ?? 0,
                        RecoveryIndex = readinessDocument.Contributors.RecoveryIndex ?? 0,
                        RestingHeartRate = readinessDocument.Contributors.RestingHeartRate ?? 0,
                        SleepBalance = readinessDocument.Contributors.SleepBalance ?? 0,
                    };
                }
                else
                {
                    readinessData = new()
                    {
                        ReadinessScore = -1,
                        TemperatureDeviation = 0,
                        ActivityBalance = 0,
                        BodyTemperature = 0,
                        HrvBalance = 0,
                        PreviousDayActivity = 0,
                        PreviousNight = 0,
                        RecoveryIndex = 0,
                        RestingHeartRate = 0,
                        SleepBalance = 0,
                    };
                }


                // Build Database Object
                DailyOuraData dailyOuraInfo = new()
                {
                    ObjectID = Guid.NewGuid(),
                    UserID = userGuid,
                    Date = currentDate,
                    SyncDate = DateOnly.FromDateTime(DateTime.Now),
                    ActivityData = activityData,
                    ResilienceData = resilienceData,
                    SleepData = sleepData,
                    ReadinessData = readinessData,
                    ActivityJson = activityJson,
                    ResilienceJson = resilienceJson,
                    SleepJson = sleepJson,
                    ReadinessJson = readinessJson,
                };

                // Add or update database
                if (OuraStateInfoObjects.Any(o => o.Date == dailyOuraInfo.Date))
                {
                    var existingState = await _context.DailyOuraDatas.FirstOrDefaultAsync(w => w.Date == dailyOuraInfo.Date && w.UserID == userGuid);
                    if (existingState != null)
                    {
                        existingState.SyncDate = dailyOuraInfo.SyncDate;
                        existingState.ActivityData = dailyOuraInfo.ActivityData;
                        existingState.SleepData = dailyOuraInfo.SleepData;
                        existingState.ReadinessData = dailyOuraInfo.ReadinessData;
                        existingState.ResilienceData = dailyOuraInfo.ResilienceData;

                        existingState.ActivityJson = dailyOuraInfo.ActivityJson;
                        existingState.SleepJson = dailyOuraInfo.SleepJson;
                        existingState.ReadinessJson = dailyOuraInfo.ReadinessJson;
                        existingState.ResilienceJson = dailyOuraInfo.ResilienceJson;
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        _context.DailyOuraDatas.Add(dailyOuraInfo);
                        await _context.SaveChangesAsync();

                    }
                    // If the while loop is never entered, return false to indicate nothing was synced

                }
                else
                {
                    _context.DailyOuraDatas.Add(dailyOuraInfo);
                    await _context.SaveChangesAsync();

                }
                currentDate = currentDate.AddDays(1);
            }
            return Result.Success();
        }

        /// <summary>
        /// This method takes a url and a user personal access token. It calls the oura api the necessary number of times (depending on
        ///   number of objects oura can send, next_token is used to indicate whether api must be called more than once), storing those objects 
        ///   into a list and returning that list and the original unserialized json. The objects recieved and stored is to be defined by the user. 
        /// </summary>
        /// <typeparam name="T">Generic type, will be defined by user (what type of object we are going to serialize/get from oura)</typeparam>
        /// <param name="url">Url for api call</param>
        /// <param name="userPersonalToken">oura personal access token</param>
        /// <returns>List of created objects, accumulated raw json from oura </returns>
        private async Task<(List<T>, string)> GetOuraDataFromApiAsync<T>(string url, string userPersonalToken)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true // deserialization works even if JSON properties are camelCase
            };
            var allItems = new List<T>();
            string? nextToken = null;
            StringBuilder allJson = new();

            do
            {
                string requestUrl = nextToken == null ? url : $"{url}&next_token={nextToken}";

                var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", userPersonalToken);

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync();
                allJson.Append(jsonResponse);
                Console.WriteLine($"Response: {jsonResponse}");
                var apiResponse = JsonSerializer.Deserialize<OuraApiResponse<T>>(jsonResponse);

                if (apiResponse != null)
                {
                    allItems.AddRange(apiResponse.Data);

                    nextToken = apiResponse.NextToken;
                }


            } while (nextToken != null);

            return (allItems, allJson.ToString());
        }

        /// <summary>
        /// Looks up (in the db) the (privateKey) users personal access token for the oura application 
        /// </summary>
        /// <param name="privateKey">User Private Key for db lookup</param>
        /// <returns>string</returns>
        private async Task<string> GetUserPersonalTokenAsync(Guid privateKey)
        {
            var apiAccessToken = await _context.ApiAccessTokens.FirstOrDefaultAsync(a => a.UserID == privateKey && a.ApplicationName == APPLICATION_NAME);
            if (apiAccessToken != null)
            {
                return apiAccessToken.PersonalAccessToken;
            }
            else
            {
                return "";
            }

        }

        /// <summary>
        /// Retrieves a list of DailyOuraDataDTO for the specified date range.
        /// </summary>
        public async Task<List<DailyOuraDataDTO>> GetDailyOuraInfoRangeAsync(
            IdentityUser user,
            DateRangeRequest dateRange)
        {
            var userGuid = Guid.Parse(user.Id);
            var start = dateRange.StartDate;
            var end = dateRange.EndDate;

            // Query persisted entries
            var entities = await _context.DailyOuraDatas
                .Where(x => x.UserID == userGuid
                         && x.Date >= start
                         && x.Date <= end)
                .ToListAsync();

            // Map to DTOs
            var dtos = entities.Select(dto => new DailyOuraDataDTO
            {
                ObjectID = dto.ObjectID,
                Date = dto.Date,
                ResilienceData = dto.ResilienceData,
                ReadinessData = dto.ReadinessData,
                SleepData = dto.SleepData,
                ActivityData = dto.ActivityData,
            })
            .ToList();

            return dtos;
        }

        public async Task<Result<List<DailyOuraDataDTO>>> GetDailyOuraInfosAsync(IdentityUser user, DateRangeRequest dateRange)
        {
            var userGuid = Guid.Parse(user.Id);

            var dailyOuraInfos = await _context.DailyOuraDatas
                .AsNoTracking()
                .Where(x => x.UserID == userGuid && x.Date >= dateRange.StartDate && x.Date <= dateRange.EndDate)
                .Select(dto => new DailyOuraDataDTO
                {
                    ObjectID = dto.ObjectID,
                    Date = dto.Date,
                    ResilienceData = dto.ResilienceData,
                    ReadinessData = dto.ReadinessData,
                    SleepData = dto.SleepData,
                    ActivityData = dto.ActivityData,
                })
                .ToListAsync();

            return Result<List<DailyOuraDataDTO>>.Success(dailyOuraInfos);
        }

    }
    /// <summary>
    /// DTO object used for determining when the Oura Data for a given date was synced. Used to determine if a day's info needs to be updated or if it is final.
    /// </summary>
    /// <param name="ObjectID"></param>
    /// <param name="Date"></param>
    /// <param name="SyncDate"></param>
    public record DailyOuraInfoDto(Guid ObjectID, DateOnly Date, DateOnly SyncDate);
    /// <summary>
    /// Object to help handle reception of JSON from Oura Open API
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class OuraApiResponse<T>
    {
        [JsonPropertyName("data")]
        public required List<T> Data { get; set; } = new List<T>();
        public string? NextToken { get; set; }
    }

    /// <summary>
    /// Object to hold a Daily Oura Info for the frontend (doesnt contain some of the backend oriented properies)
    /// </summary>
    public class DailyOuraDataDTO
    {
        public Guid ObjectID { get; init; }
        public DateOnly Date { get; set; }

        public ResilienceData? ResilienceData { get; set; }
        public ActivityData? ActivityData { get; set; }
        public SleepData? SleepData { get; set; }
        public ReadinessData? ReadinessData { get; set; }

    }
}