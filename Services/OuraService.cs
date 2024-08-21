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

        public async Task<DailyOuraInfo?> GetDailyOuraInfoAsync(string userID, DateOnly date){
            var privateKey = await GetUserPrivateKey(userID);
            return await _context.DailyOuraInfos.FirstOrDefaultAsync(x => x.UserID == privateKey && x.Date == date);    
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

            // Get Dto objects representing the ouraInfos from the past 'daysPrior'
            var OuraStateInfoObjects = await _context.DailyOuraInfos.Where(o => o.Date >= date.AddDays(-daysPrior) && o.Date <= date && o.UserID == privateKey).
            Select(o => new DailyOuraInfoDto(
                o.ObjectID,
                o.Date,
                o.SyncDate
            )).ToListAsync();

            OuraStateInfoObjects = [.. OuraStateInfoObjects.OrderBy(o => o.Date)];

            // Determine how far back I need to go to pull oura data
            DateOnly earliestDate = date.AddDays(-daysPrior);
            for (int i = 0; i < OuraStateInfoObjects.Count; i++)
            {
                if (OuraStateInfoObjects[i].SyncDate <= OuraStateInfoObjects[i].Date)
                {
                    earliestDate = OuraStateInfoObjects[i].Date;
                }
            }

            // Call Oura APi's to recieve stuctured data
            var activityUrl = "https://api.ouraring.com/v2/usercollection/daily_activity?start_date=" + earliestDate.ToString("yyyy-MM-dd") + "&end_date=" + date.ToString("yyyy-MM-dd");
            var resilienceUrl = "https://api.ouraring.com/v2/usercollection/daily_resilience?start_date=" + earliestDate.ToString("yyyy-MM-dd") + "&end_date=" + date.ToString("yyyy-MM-dd");
            var sleepUrl = "https://api.ouraring.com/v2/usercollection/daily_sleep?start_date=" + earliestDate.ToString("yyyy-MM-dd") + "&end_date=" + date.ToString("yyyy-MM-dd");         
            var readinessUrl = "https://api.ouraring.com/v2/usercollection/daily_readiness?start_date=" + earliestDate.ToString("yyyy-MM-dd") + "&end_date=" + date.ToString("yyyy-MM-dd");
        
            (List<OuraDailyActivityDocument> activityDocuments, string activityJson) = await GetOuraDataFromApiAsync<OuraDailyActivityDocument>(activityUrl, userPersonalToken);
            (List<OuraDailyResilienceDocument> resilienceDocuments, string resilienceJson) = await GetOuraDataFromApiAsync<OuraDailyResilienceDocument>(resilienceUrl, userPersonalToken);
            (List<OuraDailySleepDocument> sleepDocuments, string sleepJson) = await GetOuraDataFromApiAsync<OuraDailySleepDocument>(sleepUrl, userPersonalToken);
            (List<OuraDailyReadinessDocument> readinessDocuments, string readinessJson) = await GetOuraDataFromApiAsync<OuraDailyReadinessDocument>(readinessUrl, userPersonalToken);

            var numberDays = date.DayNumber - earliestDate.DayNumber + 1;

            for(int i = 0; i < numberDays-1; i++){
                // Build pieces of object

                ActivityData activityData = new(){
                    ActivityScore = activityDocuments[i].Score ?? 0,
                    Steps = activityDocuments[i].Steps,
                    ActiveCalories = activityDocuments[i].ActiveCalories,
                    TotalCalories = activityDocuments[i].TotalCalories,
                    TargetCalories = activityDocuments[i].TargetCalories,
                    MeetDailyTargets = activityDocuments[i].Contributors.MeetDailyTargets ?? 0,
                    MoveEveryHour = activityDocuments[i].Contributors.MoveEveryHour ?? 0,
                    RecoveryTime = activityDocuments[i].Contributors.RecoveryTime ?? 0,
                    StayActive = activityDocuments[i].Contributors.StayActive ?? 0,
                    TrainingFrequency = activityDocuments[i].Contributors.TrainingFrequency ?? 0,
                    TrainingVolume = activityDocuments[i].Contributors.TrainingVolume ?? 0,
                };

                ResilienceData resilienceData = new(){
                    SleepRecovery = resilienceDocuments[i].Contributors.SleepRecovery,
                    DaytimeRecovery = resilienceDocuments[i].Contributors.DaytimeRecovery,
                    Stress = resilienceDocuments[i].Contributors.Stress,
                    ResilienceLevel = resilienceDocuments[i].Level,
                };

                SleepData sleepData = new(){
                    SleepScore = sleepDocuments[i].Score ?? 0,
                    DeepSleep = sleepDocuments[i].Contributors.DeepSleep ?? 0,
                    Efficiency = sleepDocuments[i].Contributors.Efficiency ?? 0,
                    Latency = sleepDocuments[i].Contributors.Latency ?? 0,
                    RemSleep = sleepDocuments[i].Contributors.RemSleep ?? 0,
                    Restfulness = sleepDocuments[i].Contributors.Restfulness ?? 0,
                    Timing = sleepDocuments[i].Contributors.Timing ?? 0,
                    TotalSleep = sleepDocuments[i].Contributors.TotalSleep ?? 0,
                };

                ReadinessData readinessData = new(){
                    ReadinessScore = readinessDocuments[i].Score ?? 0,
                    TemperatureDeviation = readinessDocuments[i].TemperatureDeviation ?? 0,
                    ActivityBalance = readinessDocuments[i].Contributors.ActivityBalance ?? 0,
                    BodyTemperature = readinessDocuments[i].Contributors.BodyTemperature ?? 0,
                    HrvBalance = readinessDocuments[i].Contributors.HrvBalance ?? 0,
                    PreviousDayActivity = readinessDocuments[i].Contributors.PreviousDayActivity ?? 0,
                    PreviousNight = readinessDocuments[i].Contributors.PreviousNight ?? 0,
                    RecoveryIndex = readinessDocuments[i].Contributors.RecoveryIndex ?? 0,
                    RestingHeartRate = readinessDocuments[i].Contributors.RestingHeartRate ?? 0,
                    SleepBalance = readinessDocuments[i].Contributors.SleepBalance ?? 0,
                };

                // Build Database Object
                DailyOuraInfo dailyOuraInfo = new()
                {
                    ObjectID = Guid.NewGuid(),
                    UserID = privateKey,
                    Date = activityDocuments[i].Day,
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

                _context.DailyOuraInfos.Add(dailyOuraInfo);
                await _context.SaveChangesAsync();
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
        public required List<T> Data { get; set; }= new List<T>();
        public string? NextToken { get; set; }
    }
}