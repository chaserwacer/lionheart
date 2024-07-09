using lionheart.Data;
using lionheart.WellBeing;
using Microsoft.EntityFrameworkCore;
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
        public async Task<LionheartUser> CreateProfile(CreateProfileRequest req, string userID){
            // Lookup identity user from model context

            // lookup lionheart user that has the userID
                // if dont have lionheartuser, create new lHuser and set identity user to iDUser I got above, and copying all the req properties 


            // if does exist throw exeption
        }

    }
}
