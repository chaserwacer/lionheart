using lionheart.Controllers;
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

namespace lionheart.Services
{
    public class ActivityService : IActivityService
    {
        private readonly ModelContext _context;

        public ActivityService(ModelContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get activities from starting date to ending date. This method is run such that start is the earlier date, end is the later date (closer to present).
        /// </summary>
        /// <param name="userID">user id</param>
        /// <param name="start">earlier date</param>
        /// <param name="end">later date</param>
        /// <returns>List of activities </returns>
        public async Task<List<Activity>> GetActivitiesAsync(string userID, DateOnly start, DateOnly end){
            var privateKey = getUserPrivateKey(userID).Result;
            DateTime startDateTime = start.ToDateTime(new TimeOnly(0, 0)); 
            DateTime endDateTime = end.ToDateTime(new TimeOnly(23, 59, 59));
            return await _context.Activities.Where(a => a.DateTime >= startDateTime && a.DateTime <= endDateTime && a.UserID == privateKey).Include(a => a.RideDetails).Include(a => a.LiftDetails).Include(a => a.RunWalkDetails).ToListAsync();
        }


        /// <summary>
        /// Get number of minutes of actvivity in time span from start date to end date.
        /// </summary>
        /// <param name="userID">user id</param>
        /// <param name="start">earlier date</param>
        /// <param name="end">later date</param>
        /// <returns>int num minuntes </returns>
        public async Task<int> GetActivityMinutesAsync(string userID, DateOnly start, DateOnly end){
            var privateKey = getUserPrivateKey(userID).Result;
            DateTime startDateTime = start.ToDateTime(new TimeOnly(0, 0)); 
            DateTime endDateTime = end.ToDateTime(new TimeOnly(23, 59, 59));
            var activities = await _context.Activities.Where(a => a.DateTime >= startDateTime && a.DateTime <= endDateTime && a.UserID == privateKey).ToListAsync();
            int minutes = 0;
            foreach (var activity in activities){
                minutes += activity.TimeInMinutes;
            }
            return minutes;
                
        } 

        /// <summary>
        /// Get ratio of lifts to rides to run/walk during period from start date to end date
        /// </summary>
        /// <param name="userID">user id</param>
        /// <param name="start">earlier date</param>
        /// <param name="end">later date</param>
        /// <returns>int num minuntes </returns>
        public async Task<ActivityTypeRatioDto> GetActivityTypeRatioAsync(string userID, DateOnly start, DateOnly end){
            var activites = await GetActivitiesAsync(userID, start, end);
            var numRunWalk = 0;
            var numRide = 0;
            var numLift = 0;
            foreach(var activity in activites){
                if(activity.RunWalkDetails is not null){
                    numRunWalk++;
                }
                else if(activity.LiftDetails is not null){
                    numLift++;
                }
                else if(activity.RideDetails is not null){
                    numRide++;
                }
            }
            return new ActivityTypeRatioDto(numLift, numRunWalk, numRide);
            
            
        }

        public async Task<Activity> AddActivityAsync(string userID, CreateActivityRequest activityRequest){
            var privateKey = getUserPrivateKey(userID).Result;
            Activity activity = new(){
                ActivityID = Guid.NewGuid(),
                UserID = privateKey,
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
            return activity;
        }
        public async Task<Activity> AddRunWalkActivityAsync(string userID, CreateRunWalkRequest activityRequest){
            var privateKey = getUserPrivateKey(userID).Result;
            var activityID = Guid.NewGuid();
            RunWalkDetails runWalkDetails = new(){
                ActivityID = activityID,
                Distance = activityRequest.Distance,
                ElevationGain = activityRequest.ElevationGain,
                AveragePaceInSeconds = activityRequest.AveragePaceInSeconds,
                MileSplitsInSeconds = activityRequest.MileSplitsInSeconds,
                RunType = activityRequest.RunType,
            };
            Activity activity = new(){
                ActivityID = activityID,
                UserID = privateKey,
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
            return activity;
        }
        public async Task<Activity> AddRideActivityAsync(string userID, CreateRideRequest activityRequest){
            var privateKey = getUserPrivateKey(userID).Result;
            var activityID = Guid.NewGuid();
            RideDetails rideDetails = new(){
                ActivityID = activityID, 
                Distance = activityRequest.Distance,
                ElevationGain = activityRequest.ElevationGain,
                AveragePower = activityRequest.AveragePower,
                AverageSpeed = activityRequest.AverageSpeed,
                RideType = activityRequest.RideType,
            };

            Activity activity = new(){
                ActivityID = activityID,
                UserID = privateKey,
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
            return activity;
        }
        public async Task<Activity> AddLiftActivityAsync(string userID, CreateLiftRequest activityRequest ){
            var privateKey = getUserPrivateKey(userID).Result;
            var activityID = Guid.NewGuid();
            LiftDetails liftDetails = new(){
                ActivityID = activityID, 
                Tonnage = activityRequest.Tonnage,
                LiftType = activityRequest.LiftType,
                LiftFocus = activityRequest.LiftFocus,
            };
            Activity activity = new(){
                ActivityID = activityID,
                UserID = privateKey,
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
            return activity;
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
    }
}