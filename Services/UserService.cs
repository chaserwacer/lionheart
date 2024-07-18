using lionheart.Controllers;
using lionheart.Data;
using lionheart.WellBeing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace lionheart.Services
{
    public class UserService : IUserService
    {
        private readonly ModelContext _context;

        public UserService(ModelContext context)
        {
            _context = context;
        }

        public async Task<LionheartUser> CreateProfileAsync(CreateProfileRequest req, string userID)
        {
            if (HasCreatedProfileAsync(userID).Result.Item1) { throw new InvalidOperationException("Attempted to create lionheart user for identity user, LHU already exists"); }
            var identityUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userID);
            if (identityUser is null) { throw new NullReferenceException("Could not locate this IdentityUser in database"); }
            var identityUserID = identityUser.Id;
            if (identityUserID == null) { throw new NullReferenceException("Identity User ID was null"); }
            var privateKey = Guid.Parse(identityUserID);
            if (identityUser is null) { throw new NullReferenceException("Identity User was null"); }

            //var lionheartUser = new LionheartUser(privateKey, identityUser, req.DisplayName, req.Age, req.Weight);
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
        /// </summary>
        /// <param name="userID">Identity User username</param>
        /// <returns>tuple containing boolean indicating method result and users display name</returns>
        public async Task<(Boolean, string)> HasCreatedProfileAsync(string? userID)
        {
            if (userID == null) { return (false, string.Empty); }
            var identityUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userID); // was originally u.email but i beleive this is better
            var privateKey = identityUser?.Id;
            if(privateKey is not null){
                var guidPrivateKey = Guid.Parse(privateKey);
                var lionheartUser = await _context.LionheartUsers.FindAsync(guidPrivateKey);
                if (lionheartUser is not null){ return (true, lionheartUser.Name);}
                else { return (false, userID); }
            }
            return (false, userID);
            
        }

        /// <summary>
        /// Persist wellness state to database associated with the current user FOR TODAY
        /// </summary>
        /// <param name="req">Object holding wellness state values</param>
        /// <param name="userID">Identity User username</param>
        /// <returns>Created wellness stat</returns>
        public async Task<WellnessState> AddWellnessStateAsync(CreateWellnessStateRequest req, string userID)
        {
            var privateKey = getUserPrivateKey(userID).Result;
            DateOnly todaysDate = DateOnly.FromDateTime(DateTime.Now);
            
            // Check if already exists
            var existingState = _context.WellnessStates.FirstOrDefaultAsync(w => w.Date == todaysDate && w.UserID == privateKey);
            if (existingState.Result != null){
                existingState.Result.EnergyScore = req.Energy;
                existingState.Result.MoodScore = req.Mood;
                existingState.Result.StressScore = req.Stress;
                existingState.Result.MotivationScore = req.Motivation;
                double total = (double)(req.Mood + req.Energy + req.Motivation + (6 - req.Stress));
                int decimalPlaces = 2;
                int numComponents = 4;
                existingState.Result.OverallScore = Math.Round(total/numComponents, decimalPlaces);
                await _context.SaveChangesAsync();
                return existingState.Result;
            }
            else{
                WellnessState wellnessState = new WellnessState(privateKey, req.Motivation, req.Stress, req.Mood, req.Energy);
                _context.WellnessStates.Add(wellnessState);
                await _context.SaveChangesAsync();
                return wellnessState;   
            }

                 
        }

        /// <summary>
        /// Get the Wellness State for a given user on date
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="date"></param>
        /// <returns></returns>
        public async Task<WellnessState> GetWellnessStateAsync(string userID, DateOnly date)
        {
            var privateKey = getUserPrivateKey(userID).Result;
            return await _context.WellnessStates.FirstOrDefaultAsync(w => w.Date == date && w.UserID == privateKey) ?? new WellnessState(privateKey, 1, 1, 1, 1){ OverallScore = -1};   
           
        }

        /// <summary>
        /// Helper method to get the guid private key for a given user
        /// </summary>
        private async Task<Guid> getUserPrivateKey(string userID){
            var identityUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userID);
            var privateKey = identityUser?.Id;
            if (privateKey is null){throw new NullReferenceException("User private key was null."); }
            return Guid.Parse(privateKey);
        }

        /// <summary>
        /// Return list of wellness states from the last X days, starting from today
        /// </summary>
        public async Task<List<WellnessState>> GetLastXWellnessStatesAsync(string userID, int X){
            var privateKey = getUserPrivateKey(userID).Result;
            DateOnly endPeriod = DateOnly.FromDateTime(DateTime.Now).AddDays(-X);
            return await _context.WellnessStates.Where(w => w.Date >= endPeriod && w.UserID == privateKey).ToListAsync();
        }
    }
}
