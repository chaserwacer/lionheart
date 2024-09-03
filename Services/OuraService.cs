using System.Net.Http.Headers;
using lionheart.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using lionheart.Model.Oura.Dto;
using lionheart.Model.Oura;
using System.Text;
using System.Text.Json.Serialization;

namespace lionheart.Services
{
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

        public async Task<FrontendDailyOuraInfo> GetDailyOuraInfoAsync(string userID, DateOnly date)
        {
            var privateKey = await GetUserPrivateKey(userID);
            var dto = await _context.DailyOuraInfos.FirstOrDefaultAsync(x => x.UserID == privateKey && x.Date == date) ?? null;
            if (dto != null)
            {
                return new FrontendDailyOuraInfo()
                {
                    ObjectID = dto.ObjectID,
                    Date = dto.Date,
                    ResilienceData = dto.ResilienceData,
                    ReadinessData = dto.ReadinessData,
                    SleepData = dto.SleepData,
                    ActivityData = dto.ActivityData,
                };
            }
            else
            {
                return new FrontendDailyOuraInfo()
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
                };
            }
        }


        /// <summary>
        /// This method receives and saves oura ring data from the interval (date-daysPrior) to date,
        ///  storing or updating an entry in the database. Each day has one and only one DailyOuraInfo object.
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="date">Given Starting Date</param>
        /// <param name="daysPrior"># days worth of oura data to pull from (prior to starting date)</param>
        public async Task SyncOuraAPI(string userID, DateOnly date, int daysPrior)
        {
            var privateKey = await GetUserPrivateKey(userID);
            var userPersonalToken = await GetUserPersonalTokenAsync(privateKey);


            // Delete Section
            // var objectsToDelete = _context.DailyOuraInfos.ToList();
            // _context.DailyOuraInfos.RemoveRange(objectsToDelete);
            // _context.SaveChanges();



            

            // Get Dto objects representing the ouraInfos from the past 'daysPrior'
            var beginningDate = date.AddDays(-daysPrior);
            var OuraStateInfoObjects = await _context.DailyOuraInfos.Where(o => o.Date >= beginningDate && o.Date <= date && o.UserID == privateKey).
            Select(o => new DailyOuraInfoDto(
                o.ObjectID,
                o.Date,
                o.SyncDate
            )).ToListAsync();

            OuraStateInfoObjects = [.. OuraStateInfoObjects.OrderBy(o => o.Date)];

            // Determine how far back I need to go to pull oura data
            DateOnly earliestDateToInclude = date.AddDays(-daysPrior);
            for (int i = 0; i < OuraStateInfoObjects.Count; i++)
            {
                if (OuraStateInfoObjects[i].SyncDate > OuraStateInfoObjects[i].Date)
                {
                    earliestDateToInclude = OuraStateInfoObjects[i].Date.AddDays(1);
                }
            }

            // Call Oura APi's to recieve stuctured data
            var activityUrl = "https://api.ouraring.com/v2/usercollection/daily_activity?start_date=" + earliestDateToInclude.ToString("yyyy-MM-dd") + "&end_date=" + date.ToString("yyyy-MM-dd");
            var resilienceUrl = "https://api.ouraring.com/v2/usercollection/daily_resilience?start_date=" + earliestDateToInclude.ToString("yyyy-MM-dd") + "&end_date=" + date.ToString("yyyy-MM-dd");
            var sleepUrl = "https://api.ouraring.com/v2/usercollection/daily_sleep?start_date=" + earliestDateToInclude.ToString("yyyy-MM-dd") + "&end_date=" + date.ToString("yyyy-MM-dd");
            var readinessUrl = "https://api.ouraring.com/v2/usercollection/daily_readiness?start_date=" + earliestDateToInclude.ToString("yyyy-MM-dd") + "&end_date=" + date.ToString("yyyy-MM-dd");

            if (earliestDateToInclude >= date)
            {
                return;
            }

            (List<OuraDailyActivityDocument> activityDocuments, string activityJson) = await GetOuraDataFromApiAsync<OuraDailyActivityDocument>(activityUrl, userPersonalToken);
            (List<OuraDailyResilienceDocument> resilienceDocuments, string resilienceJson) = await GetOuraDataFromApiAsync<OuraDailyResilienceDocument>(resilienceUrl, userPersonalToken);
            (List<OuraDailySleepDocument> sleepDocuments, string sleepJson) = await GetOuraDataFromApiAsync<OuraDailySleepDocument>(sleepUrl, userPersonalToken);
            (List<OuraDailyReadinessDocument> readinessDocuments, string readinessJson) = await GetOuraDataFromApiAsync<OuraDailyReadinessDocument>(readinessUrl, userPersonalToken);

            var numberDays = date.DayNumber - earliestDateToInclude.DayNumber + 1;
            ActivityData activityData;
            ResilienceData resilienceData;
            SleepData sleepData;
            ReadinessData readinessData;
            var currentDate = date.AddDays(-numberDays);

            for (int i = 0; i < numberDays - 1; i++) 
            {
                currentDate = currentDate.AddDays(1);
                // Build pieces of object
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
                else{
                     readinessData = new()
                    {
                        ReadinessScore = -1,
                        TemperatureDeviation = 0,
                        ActivityBalance =  0,
                        BodyTemperature =  0,
                        HrvBalance =  0,
                        PreviousDayActivity =  0,
                        PreviousNight =  0,
                        RecoveryIndex = 0,
                        RestingHeartRate =  0,
                        SleepBalance =  0,
                    };
                }


                // Build Database Object
                DailyOuraInfo dailyOuraInfo = new()
                {
                    ObjectID = Guid.NewGuid(),
                    UserID = privateKey,
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
                    var existingState = await _context.DailyOuraInfos.FirstOrDefaultAsync(w => w.Date == dailyOuraInfo.Date && w.UserID == privateKey);
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
                        _context.DailyOuraInfos.Add(dailyOuraInfo);
                        await _context.SaveChangesAsync();
                    }
                }
                else
                {
                    _context.DailyOuraInfos.Add(dailyOuraInfo);
                    await _context.SaveChangesAsync();
                }

            }
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
        /// Helper method to get the guid private key for a given user
        /// </summary>
        private async Task<Guid> GetUserPrivateKey(string userID)
        {
            var identityUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userID);
            var privateKey = identityUser?.Id;
            if (privateKey is null) { throw new NullReferenceException("User private key was null."); }
            return Guid.Parse(privateKey);
        }
    }

    public record DailyOuraInfoDto(Guid ObjectID, DateOnly Date, DateOnly SyncDate);
    public class OuraApiResponse<T>
    {
        [JsonPropertyName("data")]
        public required List<T> Data { get; set; } = new List<T>();
        public string? NextToken { get; set; }
    }

    public class FrontendDailyOuraInfo
    {
        public Guid ObjectID { get; init; }
        public DateOnly Date { get; set; }

        public ResilienceData? ResilienceData { get; set; }
        public ActivityData? ActivityData { get; set; }
        public SleepData? SleepData { get; set; }
        public ReadinessData? ReadinessData { get; set; }

    }
}