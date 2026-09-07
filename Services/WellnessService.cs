using lionheart.Functional;
using lionheart.Data;
using lionheart.Model.Request;
using lionheart.WellBeing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Model.Chat.Tools;

namespace lionheart.Services
{
    /// <summary>
    /// What a wellness write did. A day the athlete had not logged yet is a new record; a day
    /// they had is an overwrite of it. The two are indistinguishable in the state that comes
    /// back, and the endpoint has to tell them apart to answer 201 or 200.
    /// </summary>
    public abstract record WellnessStateWrite(WellnessState State)
    {
        public sealed record Created(WellnessState State) : WellnessStateWrite(State);

        public sealed record Updated(WellnessState State) : WellnessStateWrite(State);
    }

    /// <summary>
    /// Wellness service interface for managing wellness states.
    /// </summary>
    public interface IWellnessService
    {
        /// <summary>
        /// Fetch the <see cref="WellnessState"/> for <paramref name="user"/> on a specific <paramref name="date"/>.
        /// If no wellness state exists for that date, a blank one will be returned with default values. 
        /// </summary>
        Task<Result<WellnessState, Problem>> GetWellnessStateAsync(IdentityUser user, DateOnly date);

        /// <summary>
        /// Select all <see cref="WellnessState"/>s for the <paramref name="user"/> within the specified <paramref name="dateRange"/>.
        /// </summary>
        Task<Result<List<WellnessState>, Problem>> GetWellnessStatesAsync(IdentityUser user, DateRangeRequest dateRange);

        /// <summary>
        /// Add a new <see cref="WellnessState"/> for the <paramref name="user"/>.
        /// If a wellness state already exists for the date, it will be overwritten — which the
        /// returned <see cref="WellnessStateWrite"/> says, so the caller can answer accordingly.
        /// </summary>
        Task<Result<WellnessStateWrite, Problem>> AddWellnessStateAsync(IdentityUser user, CreateWellnessStateRequest req);
    }
    [ToolProvider]
    public class WellnessService : IWellnessService
    {
        private readonly ModelContext _context;

        public WellnessService(ModelContext context)
        {
            _context = context;
        }

        public async Task<Result<WellnessState, Problem>> GetWellnessStateAsync(IdentityUser user, DateOnly date)
        {
            var userGuid = Guid.Parse(user.Id);
            var state = await _context.WellnessStates.FirstOrDefaultAsync(w => w.Date == date && w.UserID == userGuid) ?? new WellnessState(userGuid, 1, 1, 1, 1, date) { OverallScore = -1 };
            return Result<WellnessState, Problem>.Success(state);
        }
        [Tool(Name = "GetWellnessStates", Description = "Get wellness states for user within date range.")]
        public async Task<Result<List<WellnessState>, Problem>> GetWellnessStatesAsync(IdentityUser user, DateRangeRequest dateRange)
        {
            var userGuid = Guid.Parse(user.Id);
            DateOnly startDate = DateOnly.FromDateTime(dateRange.StartDate);
            DateOnly endDate = DateOnly.FromDateTime(dateRange.EndDate);

            var states = await _context.WellnessStates
                .Where(w => w.Date >= startDate && w.Date <= endDate && w.UserID == userGuid)
                .ToListAsync();

            return Result<List<WellnessState>, Problem>.Success(states.OrderBy(w => w.Date).ToList());
        }

        public async Task<Result<WellnessStateWrite, Problem>> AddWellnessStateAsync(IdentityUser user, CreateWellnessStateRequest req)
        {
            var userGuid = Guid.Parse(user.Id);
            DateOnly selectedDate = DateOnly.FromDateTime(req.Date);

            // The constructor owns the overall-score rule, so scoring a fresh state is how the
            // overwrite path stays on that one rule instead of restating the arithmetic.
            var scored = new WellnessState(userGuid, req.Motivation, req.Stress, req.Mood, req.Energy, selectedDate);

            var existingState = await _context.WellnessStates
                .FirstOrDefaultAsync(w => w.Date == selectedDate && w.UserID == userGuid);

            var write = existingState is null ? Add(scored) : Overwrite(existingState, scored);

            await InvalidateRecentStateAsync(userGuid);
            await _context.SaveChangesAsync();

            return Result<WellnessStateWrite, Problem>.Success(write);
        }

        private WellnessStateWrite Add(WellnessState scored)
        {
            _context.WellnessStates.Add(scored);
            return new WellnessStateWrite.Created(scored);
        }

        /// <summary>
        /// One state per athlete per day: logging the same day again is a correction of what is
        /// there, never a second row.
        /// </summary>
        private static WellnessStateWrite Overwrite(WellnessState existingState, WellnessState scored)
        {
            existingState.EnergyScore = scored.EnergyScore;
            existingState.MoodScore = scored.MoodScore;
            existingState.StressScore = scored.StressScore;
            existingState.MotivationScore = scored.MotivationScore;
            existingState.OverallScore = scored.OverallScore;

            return new WellnessStateWrite.Updated(existingState);
        }

        /// <summary>
        /// Event-driven invalidation of the Athlete Context Card's volatile Recent State tier.
        /// Marking it stale (rather than recomputing here) keeps the write path cheap; the card is
        /// rebuilt lazily on the next read via <c>IAthleteContextCardService.GetOrBuildCardAsync</c>.
        /// Batched into the caller's <c>SaveChangesAsync</c> — no extra round trip.
        /// </summary>
        private async Task InvalidateRecentStateAsync(Guid userGuid)
        {
            var card = await _context.AthleteContextCards.FirstOrDefaultAsync(c => c.UserID == userGuid);
            if (card is not null)
            {
                card.RecentStateAsOf = DateTime.MinValue;
            }
        }

    }
}
