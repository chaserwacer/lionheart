using lionheart.WellBeing;
using lionheart.ActivityTracking;
using Microsoft.AspNetCore.Identity;
using lionheart.Model.DTOs;
using Ardalis.Result;

namespace lionheart.Services
{
    public interface IActivityService
    {
        /// <summary>
        /// Get all activities for a user within a specified date range.
        /// </summary>
        /// <param name="user">The user whose activities to retrieve.</param>
        /// <param name="dateRange">The date range for filtering activities.</param>
        /// <returns>A result containing a list of activities.</returns>
        Task<Result<List<Activity>>> GetActivitiesAsync(IdentityUser user, DateRangeRequest dateRange);

        /// <summary>
        /// Add a new activity for a user.
        /// </summary>
        /// <param name="user">The user to add the activity for.</param>
        /// <param name="activityRequest">The activity creation request.</param>
        /// <returns>A result containing the created activity.</returns>
        Task<Result<Activity>> AddActivityAsync(IdentityUser user, CreateActivityRequest activityRequest);

        /// <summary>
        /// Add a new run/walk activity for a user.
        /// </summary>
        /// <param name="user">The user to add the run/walk activity for.</param>
        /// <param name="activityRequest">The run/walk activity creation request.</param>
        /// <returns>A result containing the created activity.</returns>
        Task<Result<Activity>> AddRunWalkActivityAsync(IdentityUser user, CreateRunWalkRequest activityRequest);

        /// <summary>
        /// Add a new ride activity for a user.
        /// </summary>
        /// <param name="user">The user to add the ride activity for.</param>
        /// <param name="activityRequest">The ride activity creation request.</param>
        /// <returns>A result containing the created activity.</returns>
        Task<Result<Activity>> AddRideActivityAsync(IdentityUser user, CreateRideRequest activityRequest);

        /// <summary>
        /// Add a new lift activity for a user.
        /// </summary>
        /// <param name="user">The user to add the lift activity for.</param>
        /// <param name="activityRequest">The lift activity creation request.</param>
        /// <returns>A result containing the created activity.</returns>
        Task<Result<Activity>> AddLiftActivityAsync(IdentityUser user, CreateLiftRequest activityRequest);

        /// <summary>
        /// Get the total number of activity minutes for a user within a specified date range.
        /// </summary>
        /// <param name="user">The user whose activity minutes to retrieve.</param>
        /// <param name="dateRange">The date range for filtering activities.</param>
        /// <returns>A result containing the total activity minutes.</returns>
        Task<Result<int>> GetActivityMinutesAsync(IdentityUser user, DateRangeRequest dateRange);

        /// <summary>
        /// Get the ratio of different activity types (e.g., run, walk, ride, lift) for a user within a specified date range.
        /// </summary>
        /// <param name="user">The user whose activity type ratio to retrieve.</param>
        /// <param name="dateRange">The date range for filtering activities.</param>
        /// <returns>A result containing the activity type ratio.</returns>
        Task<Result<ActivityTypeRatioDto>> GetActivityTypeRatioAsync(IdentityUser user, DateRangeRequest dateRange);

        /// <summary>
        /// Get the muscle sets for a user within a specified date range.
        /// </summary>
        /// <param name="user">The user whose muscle sets to retrieve.</param>
        /// <param name="dateRange">The date range for filtering activities.</param>
        /// <returns>A result containing the muscle sets.</returns>
        Task<Result<MuscleSetsDto>> GetMuscleSetsAsync(IdentityUser user, DateRangeRequest dateRange);
    }
}