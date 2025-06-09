// using lionheart.Controllers;
// using lionheart.Data;
// using lionheart.WellBeing;
// using lionheart.ActivityTracking;
// using Microsoft.AspNetCore.Identity;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.EntityFrameworkCore.Storage;
// using Microsoft.OpenApi.Extensions;
// using System;
// using System.Collections.Generic;
// using System.Drawing;
// using System.Linq;
// using System.Threading.Tasks;
// using System.Text;
// using System.Text.Json;
// namespace lionheart.Services
// {
//     /// <summary>
//     /// Service to handle use of Phi-4 model to generate insights on athlete.
//     /// </summary>
//     public class InsightService(ModelContext context, ActivityService activityService, UserService userService, OuraService ouraService) 
//     {
//         const int TOKEN_LIMIT = 4096;
//         const int SAFE_INPUT_TOKENS = 3000; // Leave room for response



//         private readonly ModelContext _context = context;
//         private readonly ActivityService _activityService = activityService;
//         private readonly UserService _userService = userService;
//         private readonly OuraService _ouraService = ouraService;
//         private readonly Phi4Service _phi4Service = phi4Service;

//         /// <summary>
//         /// Generate an insight for a user on a given date, with a focus on <param cref="insightType"/>.
//         /// Do this by building a collection of data from the user's activities and Oura data, then sending it to the Phi-4 model.
//         /// </summary>
//         /// <param name="userID"></param>
//         /// <param name="date"></param>
//         /// <param name="insightType"></param>
//         /// <returns></returns>
//         public async Task<string> GenerateInsightAsync(string userID, DateOnly date, string insightType)
//         {
//             var privateKey = GetUserPrivateKey(userID).Result;

//             var promptComponent = "You are an expert in analyzing athlete recovery, stress, and performance. You will be given structured data containing an athlete's training sessions, perceived wellness scores, and biometric data (e.g., sleep, HRV, stress). Your task is to analyze this data and generate a personalized insight. 1. **How They Might Feel** - Analyze training load, recovery metrics, and wellness scores. Compare **subjective wellness** (self-reported) vs. **objective data** (Oura, HRV, sleep). Identify trends that could impact performance. 2. **Comparison of Wellness & Objective Data** - Look for mismatches, e.g., **high stress ratings aligning with poor sleep quality.** Detect trends in fatigue, performance drop-offs, or recovery improvements. 3. **Training Recommendation** - Provide a **specific** recommendation based on their fatigue level. Example: “Your training volume has been high, and your HRV is declining. A deload or lower-intensity session may help recovery.” Example Insight Format: **Insight:** Based on your past 7 days, you may be feeling [fatigue level] due to [training load, sleep trends]. **Wellness vs. Data:** Your self-reported stress is high, aligning with a **5% decline in sleep score**, indicating recovery strain. **Recommendation:** Adjust intensity today to allow proper recovery. Consider a **restorative session or mobility work.** Athlete Data (JSON Format): {athlete_data_here}";

//             var dataComponentJSON = GeneratetUserData(userID, date);

//             var promptComponentJSON = JsonSerializer.Serialize(promptComponent);

//             var prompt = promptComponentJSON + dataComponentJSON;
//             return await _phi4Service.GenerateResponseAsync(userID, prompt);
            
//         }

//         private string GeneratetUserData(string userID, DateOnly date){
//             var ouraPastWeek = new List<FrontendDailyOuraInfo>();
//             for (int x = 0; x < 7; x++)
//             {
//                 var ouraInfo = _ouraService.GetDailyOuraInfoAsync(userID, date.AddDays(-x)).Result;
//                 ouraPastWeek.Add(ouraInfo);
//             }
//             var activitiesPastWeek = _activityService.GetActivitiesAsync(userID, date.AddDays(-7), date).Result;

//             var wellnessPastWeek = new List<WellnessState>();
//             for (int x = 0; x < 7; x++)
//             {
//                 var wellnessState = _userService.GetWellnessStateAsync(userID, date.AddDays(-x)).Result;
//                 wellnessPastWeek.Add(wellnessState);
//             }

//             var dataComponentJSON = new StringBuilder();
//             var i = 0;
//             while(!DataIsTooLarge(dataComponentJSON.ToString()))
//             {
//                 var dataCJPrAddition = new string(dataComponentJSON.ToString());
//                 dataComponentJSON.Append(JsonSerializer.Serialize(ouraPastWeek[i]));
//                 if (DataIsTooLarge(dataComponentJSON.ToString()))
//                 {
//                     return dataCJPrAddition;
//                 }


//                 dataCJPrAddition = new string(dataComponentJSON.ToString());
//                 dataComponentJSON.Append(JsonSerializer.Serialize(activitiesPastWeek[i]));
//                 if (DataIsTooLarge(dataComponentJSON.ToString()))
//                 {
//                     return dataCJPrAddition;
//                 }

//                 dataCJPrAddition = new string(dataComponentJSON.ToString());
//                 dataComponentJSON.Append(JsonSerializer.Serialize(wellnessPastWeek[i]));
//                 if (DataIsTooLarge(dataComponentJSON.ToString()))
//                 {
//                     return dataCJPrAddition;
//                 }

                
//                 i++;
//             }
//             return dataComponentJSON.ToString();

            
//         }

//         /// <summary>
//         /// Helper method to get the guid private key for a given user
//         /// </summary>
//         private async Task<Guid> GetUserPrivateKey(string userID)
//         {
//             var identityUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userID);
//             var privateKey = (identityUser?.Id) ?? throw new NullReferenceException("User private key was null.");
//             return Guid.Parse(privateKey);
//         }

//         private static int EstimateTokenCount(string text)
//         {
//             // Approximate token count based on characters
//             return text.Length / 4;
//         }

//         /// <summary>
//         /// Check if the data is too large to send to the Phi-4 model
//         /// </summary>
//         /// <param name="jsonData"></param>
//         /// <returns>boolean </returns>
//         private static bool DataIsTooLarge(string jsonData)
//         {
//             int tokenCount = EstimateTokenCount(jsonData);
//             return tokenCount > SAFE_INPUT_TOKENS;
//         }

//     }

// }
