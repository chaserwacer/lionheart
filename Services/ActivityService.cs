using lionheart.Data;
using lionheart.WellBeing;
using lionheart.ActivityTracking;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using lionheart.Model.DTOs;
using Ardalis.Result;
using Mapster;
using lionheart.Model.Training;

namespace lionheart.Services
{
    public interface IActivityService
    {
        Task<Result<ActivityDTO>> GetActivityAsync(IdentityUser user, Guid activityID);
        Task<Result<List<ActivityDTO>>> GetActivitiesAsync(IdentityUser user, DateRangeRequest dateRange);
        Task<Result<ActivityDTO>> AddActivityAsync(IdentityUser user, CreateActivityRequest activityRequest);
        Task<Result<ActivityDTO>> UpdateActivityAsync(IdentityUser user, UpdateActivityRequest activityRequest);
        Task<Result> DeleteActivityAsync(IdentityUser user, Guid activityID);
    }

    public class ActivityService : IActivityService
    {
        private readonly ModelContext _context;

        public ActivityService(ModelContext context)
        {
            _context = context;
        }

        public async Task<Result<ActivityDTO>> AddActivityAsync(IdentityUser user, CreateActivityRequest activityRequest)
        {
            var userId = Guid.Parse(user.Id);
            var activity = new Activity
            {
                ActivityID = Guid.NewGuid(),
                UserID = userId,
                DateTime = activityRequest.DateTime,
                TimeInMinutes = activityRequest.TimeInMinutes,
                CaloriesBurned = activityRequest.CaloriesBurned,
                Name =  activityRequest.Name,
                UserSummary = activityRequest.UserSummary,
                PerceivedEffortRatings = activityRequest.PerceivedEffortRatings,
                ActivityDetails = activityRequest.ActivityDetails
            };
            await _context.Activities.AddAsync(activity);
            await _context.SaveChangesAsync();
            return Result.Success(activity.Adapt<ActivityDTO>());
        }

        public async Task<Result> DeleteActivityAsync(IdentityUser user, Guid activityID)
        {
            var userId = Guid.Parse(user.Id);
            var activty = await _context.Activities
                .FirstOrDefaultAsync(a => a.ActivityID == activityID && a.UserID == userId);
            if (activty == null)
            {
                return Result.NotFound("Activity with ID not found for the user.");
            }
            _context.Activities.Remove(activty);
            await _context.SaveChangesAsync();
            return Result.Success();
        }

        public async Task<Result<List<ActivityDTO>>> GetActivitiesAsync(IdentityUser user, DateRangeRequest dateRange)
        {
            var userId = Guid.Parse(user.Id);
            DateTime startDate = dateRange.StartDate.ToDateTime(TimeOnly.MinValue);
            DateTime endDate = dateRange.EndDate.ToDateTime(TimeOnly.MaxValue);
            var activities = await _context.Activities
                .AsNoTracking()
                .Where(a => a.UserID == userId && a.DateTime >= startDate && a.DateTime <= endDate)
                .ToListAsync();
            return Result.Success(activities.Adapt<List<ActivityDTO>>());
        }

        public async Task<Result<ActivityDTO>> GetActivityAsync(IdentityUser user, Guid activityID)
        {
          var userId = Guid.Parse(user.Id);
          var acitivtity = _context.Activities.AsNoTracking()
                .FirstOrDefaultAsync(a => a.ActivityID == activityID && a.UserID == userId);
            if (acitivtity == null)
            {
                return Result.NotFound("Activity with ID not found for the user.");
            }
            return Result.Success(acitivtity.Adapt<ActivityDTO>());
        }

        public async Task<Result<ActivityDTO>> UpdateActivityAsync(IdentityUser user, UpdateActivityRequest activityRequest)
        {
            var userId = Guid.Parse(user.Id);
            var activity = await _context.Activities
                .FirstOrDefaultAsync(a => a.ActivityID == activityRequest.ActivityID && a.UserID == userId);
            if (activity == null)
            {
                return Result.NotFound("Activity with ID not found for the user.");
            }
            activity.DateTime =  activityRequest.DateTime;
            activity.TimeInMinutes = activityRequest.TimeInMinutes; 
            activity.CaloriesBurned = activityRequest.CaloriesBurned;
            activity.Name = activityRequest.Name;
            activity.UserSummary = activityRequest.UserSummary;
            if (activityRequest.PerceivedEffortRatings != null)
            {
                activity.PerceivedEffortRatings = new PerceivedEffortRatings
                {
                    AccumulatedFatigue = activityRequest.PerceivedEffortRatings.AccumulatedFatigue,
                    DifficultyRating = activityRequest.PerceivedEffortRatings.DifficultyRating,
                    EngagementRating = activityRequest.PerceivedEffortRatings.EngagementRating,
                    ExternalVariablesRating = activityRequest.PerceivedEffortRatings.ExternalVariablesRating
                };
            }
            if (activityRequest.ActivityDetails != null)
            {
                activity.ActivityDetails = new ActivityDetails
                {
                    Distance = activityRequest.ActivityDetails.Distance,
                    ElevationGain = activityRequest.ActivityDetails.ElevationGain,
                    AveragePower = activityRequest.ActivityDetails.AveragePower,
                    AverageSpeed = activityRequest.ActivityDetails.AverageSpeed,
                    Type = activityRequest.ActivityDetails.Type
                };
            }
            
            await _context.SaveChangesAsync();
            return Result.Success(activity.Adapt<ActivityDTO>());
        }
    }


}