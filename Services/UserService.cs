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
        private readonly UserContext _context;

        public UserService(UserContext context)
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

        public async Task<User> CreateUserAsync(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<User?> GetUserAsync(Guid userId)
        {
            return await _context.Users.FindAsync(userId);
        }
    }
}
