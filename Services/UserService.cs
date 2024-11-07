using lionheart.Controllers;
using lionheart.Data;
using lionheart.WellBeing;
using lionheart.Controllers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.OpenApi.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace lionheart.Services
{
    /// <summary>
    /// The UserService class acts on the service layer to handle business logic and interactions with the database, after being called upon via the UserController.
    /// This class is the most hectic in terms of the different things it 'does'. It handles profile creation, which is a process that meshes with 
    /// account creation via ASP.Net Identity User services. A user first registers through ASP.Net to create an Identity User (those code methods are outsourced), and then must create their actual 
    /// user profile/LionheartUser profile (with display name, etc.). A LionheartUser contains that identity user. See model context for more details. 
    /// This class also handles creating/editing user WellnessStates, as well as adding the personal access token for Oura.  
    /// 
    /// TODO: This class is to be taken and manipulated such that it only contains user centered methods, with those other methods moving to their own service class and then their own controller class. 
    /// </summary>
    public class UserService : IUserService
    {
        private readonly ModelContext _context;

        public UserService(ModelContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Create a 'lionheartUser' profile for one who is already registered as an identity user with ASP.Net.
        /// </summary>
        /// <param name="req">dto object containing values for use in profile creation</param>
        /// <param name="userID">Identity User username</param>
        /// <returns>Lionheart User</returns>
        /// <exception cref="InvalidOperationException">User Profile already exists</exception>
        /// <exception cref="NullReferenceException">ASP.Net identity user DNE</exception>
        public async Task<LionheartUser> CreateProfileAsync(CreateProfileRequest req, string userID)
        {
            if (HasCreatedProfileAsync(userID).Result.Item1) { throw new InvalidOperationException("Attempted to create lionheart user for identity user, LHU already exists"); }
            var identityUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userID);
            if (identityUser is null) { throw new NullReferenceException("Could not locate this IdentityUser in database"); }
            var identityUserID = identityUser.Id;
            if (identityUserID == null) { throw new NullReferenceException("Identity User ID was null"); }
            var privateKey = Guid.Parse(identityUserID);
            if (identityUser is null) { throw new NullReferenceException("Identity User was null"); }

            LionheartUser lionheartUser = new()
            {
                UserID = privateKey,
                IdentityUser = identityUser,
                Name = req.DisplayName,
                Age = req.Age,
                Weight = req.Weight
            };

            _context.LionheartUsers.Add(lionheartUser);
            await _context.SaveChangesAsync();
            return lionheartUser;
        }

        /// <summary>
        /// Determines whether a person with a userID has created a lionheart profile. 
        /// Used for checking how far they are in profile creation, i.e. registered with ASP.Net but not yet having created a Lionheart profile. 
        /// </summary>
        /// <param name="userID">Identity User username</param>
        /// <returns>tuple containing boolean indicating state of profile creation and users display name</returns>
        public async Task<(Boolean, string)> HasCreatedProfileAsync(string? userID)
        {
            if (userID == null) { return (false, string.Empty); }
            var identityUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userID);
            var privateKey = identityUser?.Id;
            if (privateKey is not null)
            {
                var guidPrivateKey = Guid.Parse(privateKey);
                var lionheartUser = await _context.LionheartUsers.FindAsync(guidPrivateKey);
                if (lionheartUser is not null) { return (true, lionheartUser.Name); }
                else { return (false, userID); }
            }
            return (false, userID);

        }

        /// <summary>
        /// Persist wellness state to database associated with the current user
        /// </summary>
        /// <param name="req">Object holding wellness state values</param>
        /// <param name="userID">Identity User username</param>
        /// <returns>Created wellness state</returns>
        // public async Task<WellnessState> AddWellnessStateAsync(CreateWellnessStateRequest req, string userID)
        // {
        //     var privateKey = getUserPrivateKey(userID).Result;
        //     DateOnly selectedDate = DateOnly.ParseExact(req.Date, "yyyy-MM-dd");

        //     // Check if already exists
        //     var existingState = _context.WellnessStates.FirstOrDefaultAsync(w => w.Date == selectedDate && w.UserID == privateKey);
        //     if (existingState.Result != null)
        //     {
        //         existingState.Result.EnergyScore = req.Energy;
        //         existingState.Result.MoodScore = req.Mood;
        //         existingState.Result.StressScore = req.Stress;
        //         existingState.Result.MotivationScore = req.Motivation;
        //         double total = (double)(req.Mood + req.Energy + req.Motivation + (6 - req.Stress));
        //         int decimalPlaces = 2;
        //         int numComponents = 4;
        //         existingState.Result.OverallScore = Math.Round(total / numComponents, decimalPlaces);
        //         await _context.SaveChangesAsync();
        //         return existingState.Result;
        //     }
        //     else
        //     {
        //         WellnessState wellnessState = new WellnessState(privateKey, req.Motivation, req.Stress, req.Mood, req.Energy, selectedDate);
        //         _context.WellnessStates.Add(wellnessState);
        //         await _context.SaveChangesAsync();
        //         return wellnessState;
        //     }
        // }

        // /// <summary>
        // /// Set a personal access token for a given application. Creates a token for a given application if it does not yet exist, 
        // /// updates it if it already exists.
        // /// </summary>
        // /// <param name="userID"></param>
        // /// <param name="applicationName"></param>
        // /// <param name="accessToken"></param>
        // /// <returns></returns>
        // public async Task<bool> SetPersonalApiAccessToken(string userID, string applicationName, string accessToken)
        // {
        //     var privateKey = getUserPrivateKey(userID).Result;

        //     var existingToken = _context.ApiAccessTokens.FirstOrDefault(x => x.UserID == privateKey && x.ApplicationName == applicationName);
        //     if (existingToken != null)
        //     {
        //         existingToken.PersonalAccessToken = accessToken;
        //         await _context.SaveChangesAsync();
        //         return true;
        //     }
        //     else
        //     {
        //         ApiAccessToken apiAccessToken = new()
        //         {
        //             ObjectID = Guid.NewGuid(),
        //             UserID = privateKey,
        //             ApplicationName = applicationName,
        //             PersonalAccessToken = accessToken,
        //         };

        //         _context.ApiAccessTokens.Add(apiAccessToken);
        //         await _context.SaveChangesAsync();
        //         return true;
        //     }

        // }

        // /// <summary>
        // /// Fetch the wellness state for a given user and date. 
        // /// </summary>
        // /// <param name="userID">Identity User username</param>
        // /// <param name="date"></param>
        // /// <returns>Wellness State</returns>
        // public async Task<WellnessState> GetWellnessStateAsync(string userID, DateOnly date)
        // {
        //     var privateKey = getUserPrivateKey(userID).Result;
        //     return await _context.WellnessStates.FirstOrDefaultAsync(w => w.Date == date && w.UserID == privateKey) ?? new WellnessState(privateKey, 1, 1, 1, 1, date) { OverallScore = -1 };
        // }

       

        /// <summary>
        /// Helper method for getting the private key from the database via using the identity user id.
        /// It is done in this fashion because the identity userID is stored in cookies. 
        /// </summary>
        /// <param name="userID">Identity User username</param>
        /// <returns></returns>
        /// <exception cref="NullReferenceException">User private key was null</exception>
        private async Task<Guid> getUserPrivateKey(string userID)
        {
            var identityUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userID);
            var privateKey = identityUser?.Id;
            if (privateKey is null) { throw new NullReferenceException("User private key was null."); }
            return Guid.Parse(privateKey);
        }

        /// <summary>
        /// Return list of wellness states from the last X days, starting from 'endDate', moving backwards.
        /// </summary>
        /// <param name="userID">Identity User username</param>
        /// <param name="endDate">last date we want info for</param>
        /// <param name="X">number days prior to endDate</param>
        /// <returns></returns>
        // public async Task<List<WellnessState>> GetLastXWellnessStatesAsync(string userID, DateOnly endDate, int X)
        // {
        //     var privateKey = getUserPrivateKey(userID).Result;
        //     DateOnly startDate = endDate.AddDays(-X);
        //     var states = await _context.WellnessStates.Where(w => w.Date >= startDate && w.Date <= endDate && w.UserID == privateKey).ToListAsync();
        //     return [.. states.OrderBy(w => w.Date)];
        // }

        // /// <summary>
        // /// Fetch a tuple containing the list of overall wellness scores and the list of dates for those scores for the last X days. Uses the GetLastXWellnessStatesAsync method.
        // /// </summary>
        // /// <param name="userID">Identity User username</param>
        // /// <param name="date">last date we want info for</param>
        // /// <param name="X">number days prior to date</param>
        // /// <returns></returns>
        // public async Task<(List<double>, List<string>)> GetLastXWellnessStatesGraphDataAsync(string userID, DateOnly date, int X)
        // {
        //     var states = await GetLastXWellnessStatesAsync(userID, date, X);
        //     if (states is not null)
        //     {
        //         var scoreList = states.Select(s => s.OverallScore).ToList();
        //         var dateList = states.Select(s => s.Date.DayOfWeek.GetDisplayName() + " (" + s.Date.ToString("MM/dd") + ")").ToList();

        //         return (scoreList, dateList);
        //     }
        //     return ([], []);
        //}


    }
}
