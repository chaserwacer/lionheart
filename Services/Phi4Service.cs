// using System.Net.Http.Headers;
// using lionheart.Data;
// using Microsoft.EntityFrameworkCore;
// using System.Text.Json;
// using lionheart.Model.Oura.Dto;
// using lionheart.Model.Oura;
// using System.Text;
// using System.Text.Json.Serialization;

// namespace lionheart.Services
// {
//     /// <summary>
//     /// Service to interact with the Phi4 model
//     /// </summary>
//     /// <param name="context"></param>
//     /// <param name="httpClient"></param>
//     public class Phi4Service(ModelContext context, HttpClient httpClient) : IPhi4Service
//     {
//         private readonly ModelContext _context = context;
//         private readonly HttpClient _httpClient = httpClient;

//         /// <summary>
//         /// Endpoint to generate a response from the Phi4 model
//         /// </summary>
//         /// <param name="prompt"></param>
//         /// <returns></returns>
//         public async Task<string> GenerateResponseAsync(String userID, string prompt)
//         {
//             var requestBody = new
//             {
//                 model = "phi",
//                 prompt = prompt,
//                 stream = false
//             };

//             var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

//             var response = await _httpClient.PostAsync("/generate", jsonContent);
//             response.EnsureSuccessStatusCode();

//             var responseContent = await response.Content.ReadAsStringAsync();
//             return responseContent;
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
//     }

// }