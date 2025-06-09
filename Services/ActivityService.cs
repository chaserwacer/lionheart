using lionheart.Data;
using lionheart.WellBeing;
using lionheart.ActivityTracking;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.OpenApi.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using lionheart.Model.DTOs;
using Ardalis.Result; // Add this if not already present

namespace lionheart.Services
{
    /// <summary>
    /// Activity Service contains the business logic regarding the handling and storage of activity operations. 
    /// </summary>
    public class ActivityService : IActivityService
    {
        private readonly ModelContext _context;

        public ActivityService(ModelContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<List<Activity>> GetLiftActivitiesAsync(IdentityUser user, DateRangeRequest dateRange)
        {
            var userGuid = Guid.Parse(user.Id);
            DateTime startDateTime = dateRange.StartDate.ToDateTime(TimeOnly.MinValue);
            DateTime endDateTime = dateRange.EndDate.ToDateTime(TimeOnly.MaxValue);
            return await _context.Activities.Where(a => a.DateTime >= startDateTime && a.DateTime <= endDateTime && a.UserID == userGuid && a.LiftDetails != null).Include(a => a.LiftDetails).ToListAsync();
        }

        public async Task<Result<List<Activity>>> GetActivitiesAsync(IdentityUser user, DateRangeRequest dateRange)
        {
            DateTime startDateTime = dateRange.StartDate.ToDateTime(TimeOnly.MinValue);
            DateTime endDateTime = dateRange.EndDate.ToDateTime(TimeOnly.MaxValue);
            var userGuid = Guid.Parse(user.Id);
            var activities = await _context.Activities
                .Where(a => a.DateTime >= startDateTime && a.DateTime <= endDateTime && a.UserID == userGuid)
                .Include(a => a.RideDetails)
                .Include(a => a.LiftDetails)
                .Include(a => a.RunWalkDetails)
                .ToListAsync();

            return Result<List<Activity>>.Success(activities);
        }

        public async Task<Result<Activity>> AddActivityAsync(IdentityUser user, CreateActivityRequest activityRequest)
        {
            var userGuid = Guid.Parse(user.Id);
            Activity activity = new()
            {
                ActivityID = Guid.NewGuid(),
                UserID = userGuid,
                DateTime = activityRequest.DateTime,
                TimeInMinutes = activityRequest.TimeInMinutes,
                CaloriesBurned = activityRequest.CaloriesBurned,
                Name = activityRequest.Name,
                UserSummary = activityRequest.UserSummary,
                AccumulatedFatigue = activityRequest.AccumulatedFatigue,
                DifficultyRating = activityRequest.DifficultyRating,
                EngagementRating = activityRequest.EngagementRating,
                ExternalVariablesRating = activityRequest.ExternalVariablesRating
            };

            _context.Activities.Add(activity);
            await _context.SaveChangesAsync();
            return Result<Activity>.Created(activity);
        }

        public async Task<Result<Activity>> AddRunWalkActivityAsync(IdentityUser user, CreateRunWalkRequest activityRequest)
        {
            var userGuid = Guid.Parse(user.Id);
            var activityID = Guid.NewGuid();
            RunWalkDetails runWalkDetails = new()
            {
                ActivityID = activityID,
                Distance = activityRequest.Distance,
                ElevationGain = activityRequest.ElevationGain,
                AveragePaceInSeconds = activityRequest.AveragePaceInSeconds,
                MileSplitsInSeconds = activityRequest.MileSplitsInSeconds,
                RunType = activityRequest.RunType,
            };
            Activity activity = new()
            {
                ActivityID = activityID,
                UserID = userGuid,
                DateTime = activityRequest.DateTime,
                TimeInMinutes = activityRequest.TimeInMinutes,
                CaloriesBurned = activityRequest.CaloriesBurned,
                Name = activityRequest.Name,
                UserSummary = activityRequest.UserSummary,
                AccumulatedFatigue = activityRequest.AccumulatedFatigue,
                DifficultyRating = activityRequest.DifficultyRating,
                EngagementRating = activityRequest.EngagementRating,
                ExternalVariablesRating = activityRequest.ExternalVariablesRating,
                RunWalkDetails = runWalkDetails,
            };

            _context.Activities.Add(activity);
            _context.RunWalkDetails.Add(runWalkDetails);
            await _context.SaveChangesAsync();
            return Result<Activity>.Created(activity);
        }

        public async Task<Result<Activity>> AddRideActivityAsync(IdentityUser user, CreateRideRequest activityRequest)
        {
            var userGuid = Guid.Parse(user.Id);
            var activityID = Guid.NewGuid();
            RideDetails rideDetails = new()
            {
                ActivityID = activityID,
                Distance = activityRequest.Distance,
                ElevationGain = activityRequest.ElevationGain,
                AveragePower = activityRequest.AveragePower,
                AverageSpeed = activityRequest.AverageSpeed,
                RideType = activityRequest.RideType,
            };

            Activity activity = new()
            {
                ActivityID = activityID,
                UserID = userGuid,
                DateTime = activityRequest.DateTime,
                TimeInMinutes = activityRequest.TimeInMinutes,
                CaloriesBurned = activityRequest.CaloriesBurned,
                Name = activityRequest.Name,
                UserSummary = activityRequest.UserSummary,
                AccumulatedFatigue = activityRequest.AccumulatedFatigue,
                DifficultyRating = activityRequest.DifficultyRating,
                EngagementRating = activityRequest.EngagementRating,
                ExternalVariablesRating = activityRequest.ExternalVariablesRating,
                RideDetails = rideDetails,
            };

            _context.Activities.Add(activity);
            _context.RideDetails.Add(rideDetails);
            await _context.SaveChangesAsync();
            return Result<Activity>.Created(activity);
        }

        public async Task<Result<Activity>> AddLiftActivityAsync(IdentityUser user, CreateLiftRequest activityRequest)
        {
            var userGuid = Guid.Parse(user.Id);
            var activityID = Guid.NewGuid();
            LiftDetails liftDetails = new()
            {
                ActivityID = activityID,
                Tonnage = activityRequest.Tonnage,
                LiftType = activityRequest.LiftType,
                LiftFocus = activityRequest.LiftFocus,
                QuadSets = activityRequest.QuadSets,
                HamstringSets = activityRequest.HamstringSets,
                BicepSets = activityRequest.BicepSets,
                TricepSets = activityRequest.TricepSets,
                ChestSets = activityRequest.ChestSets,
                BackSets = activityRequest.BackSets,
                ShoulderSets = activityRequest.ShoulderSets,
            };
            Activity activity = new()
            {
                ActivityID = activityID,
                UserID = userGuid,
                DateTime = activityRequest.DateTime,
                TimeInMinutes = activityRequest.TimeInMinutes,
                CaloriesBurned = activityRequest.CaloriesBurned,
                Name = activityRequest.Name,
                UserSummary = activityRequest.UserSummary,
                AccumulatedFatigue = activityRequest.AccumulatedFatigue,
                DifficultyRating = activityRequest.DifficultyRating,
                EngagementRating = activityRequest.EngagementRating,
                ExternalVariablesRating = activityRequest.ExternalVariablesRating,
                LiftDetails = liftDetails,
            };
            _context.Activities.Add(activity);
            _context.LiftDetails.Add(liftDetails);
            await _context.SaveChangesAsync();
            return Result<Activity>.Created(activity);
        }

        public async Task<Result<int>> GetActivityMinutesAsync(IdentityUser user, DateRangeRequest dateRange)
        {
            var userGuid = Guid.Parse(user.Id);
            DateTime startDateTime = dateRange.StartDate.ToDateTime(TimeOnly.MinValue);
            DateTime endDateTime = dateRange.EndDate.ToDateTime(TimeOnly.MaxValue);
            var activities = await _context.Activities
                .Where(a => a.DateTime >= startDateTime && a.DateTime <= endDateTime && a.UserID == userGuid)
                .ToListAsync();
            int minutes = 0;
            foreach (var activity in activities)
            {
                minutes += activity.TimeInMinutes;
            }
            return Result<int>.Success(minutes);
        }

        public async Task<Result<ActivityTypeRatioDto>> GetActivityTypeRatioAsync(IdentityUser user, DateRangeRequest dateRange)
        {
            var activitiesResult = await GetActivitiesAsync(user, dateRange);
            if (!activitiesResult.IsSuccess)
            {
                return Result<ActivityTypeRatioDto>.Error(activitiesResult.Errors.ToString());
            }
            var numRunWalk = 0;
            var numRide = 0;
            var numLift = 0;
            foreach (var activity in activitiesResult.Value)
            {
                if (activity.RunWalkDetails is not null)
                {
                    numRunWalk++;
                }
                else if (activity.LiftDetails is not null)
                {
                    numLift++;
                }
                else if (activity.RideDetails is not null)
                {
                    numRide++;
                }
            }
            return Result<ActivityTypeRatioDto>.Success(new ActivityTypeRatioDto(numLift, numRunWalk, numRide));
        }

        public async Task<Result<MuscleSetsDto>> GetMuscleSetsAsync(IdentityUser user, DateRangeRequest dateRange)
        {
            var activities = await GetLiftActivitiesAsync(user, dateRange);
            int quadSets = 0, hamstringSets = 0, tricepSets = 0, bicepSets = 0, shoulderSets = 0, chestSets = 0, backSets = 0;
            foreach (var activity in activities)
            {
                if (activity is not null && activity.LiftDetails is not null)
                {
                    quadSets += activity.LiftDetails.QuadSets;
                    hamstringSets += activity.LiftDetails.HamstringSets;
                    tricepSets += activity.LiftDetails.TricepSets;
                    bicepSets += activity.LiftDetails.BicepSets;
                    shoulderSets += activity.LiftDetails.ShoulderSets;
                    chestSets += activity.LiftDetails.ChestSets;
                    backSets += activity.LiftDetails.BackSets;
                }
            }
            return Result<MuscleSetsDto>.Success(new MuscleSetsDto(quadSets, hamstringSets, bicepSets, tricepSets, shoulderSets, chestSets, backSets));
        }
    }


}