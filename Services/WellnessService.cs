using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.Request;
using lionheart.WellBeing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Model.Chat.Tools;

namespace lionheart.Services
{
    /// <summary>
    /// Wellness service interface for managing wellness states.
    /// </summary>
    public interface IWellnessService
    {
        /// <summary>
        /// Fetch the <see cref="WellnessState"/> for <paramref name="user"/> on a specific <paramref name="date"/>.
        /// If no wellness state exists for that date, a blank one will be returned with default values. 
        /// </summary>
        Task<Result<WellnessState>> GetWellnessStateAsync(IdentityUser user, DateOnly date);

        /// <summary>
        /// Select all <see cref="WellnessState"/>s for the <paramref name="user"/> within the specified <paramref name="dateRange"/>.
        /// </summary>
        Task<Result<List<WellnessState>>> GetWellnessStatesAsync(IdentityUser user, DateRangeRequest dateRange);

        /// <summary>
        /// Add a new <see cref="WellnessState"/> for the <paramref name="user"/>.
        /// If a wellness state already exists for the date, it will be overwritten.
        /// </summary>
        Task<Result<WellnessState>> AddWellnessStateAsync(IdentityUser user, CreateWellnessStateRequest req);
    }
    [ToolProvider]
    public class WellnessService : IWellnessService
    {
        private readonly ModelContext _context;

        public WellnessService(ModelContext context)
        {
            _context = context;
        }

        public async Task<Result<WellnessState>> GetWellnessStateAsync(IdentityUser user, DateOnly date)
        {
            var userGuid = Guid.Parse(user.Id);
            var state = await _context.WellnessStates.FirstOrDefaultAsync(w => w.Date == date && w.UserID == userGuid) ?? new WellnessState(userGuid, 1, 1, 1, 1, date) { OverallScore = -1 };
            return Result<WellnessState>.Success(state);
        }
        [Tool(Name = "GetWellnessStates", Description = "Get wellness states for user within date range.")]
        public async Task<Result<List<WellnessState>>> GetWellnessStatesAsync(IdentityUser user, DateRangeRequest dateRange)
        {
            var userGuid = Guid.Parse(user.Id);
            DateOnly startDate = DateOnly.FromDateTime(dateRange.StartDate);
            DateOnly endDate = DateOnly.FromDateTime(dateRange.EndDate);

            var states = await _context.WellnessStates
                .Where(w => w.Date >= startDate && w.Date <= endDate && w.UserID == userGuid)
                .ToListAsync();

            return Result<List<WellnessState>>.Success(states.OrderBy(w => w.Date).ToList());
        }

        public async Task<Result<WellnessState>> AddWellnessStateAsync(IdentityUser user, CreateWellnessStateRequest req)
        {
            var userGuid = Guid.Parse(user.Id);
            DateOnly selectedDate = DateOnly.ParseExact(req.Date, "yyyy-MM-dd");

            // Check if already exists
            var existingState = await _context.WellnessStates.FirstOrDefaultAsync(w => w.Date == selectedDate && w.UserID == userGuid);
            if (existingState != null)
            {
                existingState.EnergyScore = req.Energy;
                existingState.MoodScore = req.Mood;
                existingState.StressScore = req.Stress;
                existingState.MotivationScore = req.Motivation;
                double total = (double)(req.Mood + req.Energy + req.Motivation + (6 - req.Stress));
                int decimalPlaces = 2;
                int numComponents = 4;
                existingState.OverallScore = Math.Round(total / numComponents, decimalPlaces);
                await _context.SaveChangesAsync();
                return Result<WellnessState>.Success(existingState);
            }
            else
            {
                WellnessState wellnessState = new WellnessState(userGuid, req.Motivation, req.Stress, req.Mood, req.Energy, selectedDate);
                _context.WellnessStates.Add(wellnessState);
                await _context.SaveChangesAsync();
                return Result<WellnessState>.Created(wellnessState);
            }
        }
        
    }
}
