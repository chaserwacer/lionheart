using lionheart.Controllers;
using lionheart.Data;
using lionheart.WellBeing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
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

        public async Task AddWellnessStateAsync(Guid userId, WellnessState wellnessState)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                _context.WellnessStates.Add(wellnessState);
                await _context.SaveChangesAsync();
            }
            else
            {
                // Handle the case where the user is not found
                throw new InvalidOperationException("User not found.");
            }
        }

        public async Task<List<WellnessState>> GetWellnessStatesAsync(Guid userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                return await _context.WellnessStates.Where(ws => ws.UserID == userId).ToListAsync();
            }
            else
            {
                // Handle the case where the user is not found
                throw new InvalidOperationException("User not found.");
            }
        }

        public async Task<WellnessState?> GetTodaysStateAsync(Guid userId)
        {

            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                return await _context.WellnessStates.Where(ws => ws.UserID == userId && ws.Date.Equals(DateTime.Now)).FirstOrDefaultAsync();
            }
            else
            {
                // Handle the case where the user is not found
                throw new InvalidOperationException("User not found.");
            }
        }

        // public async Task<WellnessState?> GetWeeklyWellnessAverage(Guid userId)
        // {
        //     var user = await _context.Users.FindAsync(userId);
        //     if (user != null)
        //     {
        //         var now = DateTime.UtcNow.Date;
        //         var lastWeek = now.AddDays(-7);
        //         List<WellnessState> states = await _context.WellnessStates.Where(ws => ws.UserID == user.UserID && ws.Date.CompareTo(lastWeek) >= 0 && ws.Date.CompareTo(now) <= 0).ToListAsync();

        //         int motivationScore = 0;
        //         int stressScore = 0;
        //         int moodScore = 0;
        //         int energyScore = 0;
        //         foreach (var state in states)
        //         {
        //             stressScore += state.StressScore;
        //             moodScore += state.MoodScore;
        //             energyScore += state.EnergyScore;
        //             motivationScore += state.MotivationScore;
        //         }
        //         return new WellnessState(userId, motivationScore, stressScore, moodScore, energyScore, "");
        //         // TODO: Would this persist to the database????
        //     }
        //     else
        //     {
        //         // Handle the case where the user is not found
        //         throw new InvalidOperationException("User not found.");
        //     }
        // }

        // public async Task<User> CreateUserAsync(User user)
        // {
        //     var existingUser = await _context.Users.FindAsync(user.UserID);
        //     if (existingUser == null)
        //     {
        //         _context.Users.Add(user);
        //         await _context.SaveChangesAsync();
        //         return user;
        //     }
        //     return existingUser;
        // }


        // public async Task<User?> GetUserAsync(Guid userId)
        // {
        //     return await _context.Users.FindAsync(userId);
        // }

        /// <summary>
        /// Attempt to create a lionheart user for an exisiting identity user
        /// </summary>
        /// <param name="req">Obj holding values to store in lionheart user</param>
        /// <param name="userID">Identity users key/email</param>
        /// <returns></returns>
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
            var lionheartUser = await _context.LionheartUsers.FindAsync(privateKey);
            if (lionheartUser is not null){ return (true, lionheartUser.Name);}
            else { return (false, userID); }
        }

    }
}
