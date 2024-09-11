using System.Diagnostics;
using lionheart;
using lionheart.Services;
using Microsoft.AspNetCore.Mvc;
namespace lionheart.Controllers
{
    /// <summary>
    /// Activity controller contains the endpoints for activitiy related functionality, calling upon ActivityService to handle the business logic. 
    /// This involves things like adding different types of activities, getting activities, and getting info about activity trends. 
    /// 
    /// This controller (as well as the activity service file) still require some more updates and functionality to be implemented. I paused on this 
    /// implementation to work on some other aspects of the project. 
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ActivityController : ControllerBase
    {
        private readonly IActivityService _activityService;
        private readonly ILogger<ActivityController> _logger;
        public ActivityController(IActivityService activityService, ILogger<ActivityController> logger)
        {
            _activityService = activityService;
            _logger = logger;
        }

        /// <summary>
        /// Track/Add activity for given user
        /// </summary>
        /// <param name="activityRequest">DTO Containing data for activity creation</param>
        /// <returns></returns>
        [HttpPost("[action]")]
        public async Task<IActionResult> AddActivity(CreateActivityRequest activityRequest)
        {
            try
            {
                if (User.Identity?.Name is null) { throw new NullReferenceException("username/key was null"); }
                var activity = await _activityService.AddActivityAsync(User.Identity.Name, activityRequest);
                return Ok(activity);
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to add Activity: {e.Message}", e);
                throw;
            }
        }

        /// <summary>
        /// Add a run/walk version of an activity
        /// </summary>
        /// <param name="activityRequest">DTO Containing data for activity creation</param>
        /// <returns></returns>
        /// <exception cref="NullReferenceException"></exception>
        [HttpPost("[action]")]
        public async Task<IActionResult> AddRunWalkActivity(CreateRunWalkRequest activityRequest)
        {
            try
            {
                if (User.Identity?.Name is null) { throw new NullReferenceException("username/key was null"); }
                var activity = await _activityService.AddRunWalkActivityAsync(User.Identity.Name, activityRequest);
                return Ok(activity);
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to add Activity: {e.Message}", e);
                throw;
            }
        }

        /// <summary>
        /// Add a (bike) ride version of an activity
        /// </summary>
        /// <param name="activityRequest">DTO Containing data for activity creation</param>
        /// <returns></returns>
        /// <exception cref="NullReferenceException"></exception>
        [HttpPost("[action]")]
        public async Task<IActionResult> AddRideActivity(CreateRideRequest activityRequest)
        {
            try
            {
                if (User.Identity?.Name is null) { throw new NullReferenceException("username/key was null"); }
                var activity = await _activityService.AddRideActivityAsync(User.Identity.Name, activityRequest);
                return Ok(activity);
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to add Activity: {e.Message}", e);
                throw;
            }
        }

        /// <summary>
        /// Add a lift version of an activity
        /// </summary>
        /// <param name="activityRequest">DTO Containing data for activity creation</param>
        /// <returns></returns>
        /// <exception cref="NullReferenceException"></exception>
        [HttpPost("[action]")]
        public async Task<IActionResult> AddLiftActivity(CreateLiftRequest activityRequest)
        {
            try
            {
                if (User.Identity?.Name is null) { throw new NullReferenceException("username/key was null"); }
                var activity = await _activityService.AddLiftActivityAsync(User.Identity.Name, activityRequest);
                return Ok(activity);
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to add Activity: {e.Message}", e);
                throw;
            }
        }

        /// <summary>
        /// Get list of activities from start date to end date
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        /// <exception cref="NullReferenceException"></exception>
        [HttpGet("[action]")]
        public async Task<List<lionheart.ActivityTracking.Activity>> GetActivities(DateOnly start, DateOnly end){
            try{
                if (User.Identity?.Name is null) { throw new NullReferenceException("username/key was null"); }
                var activities = await _activityService.GetActivitiesAsync(User.Identity.Name, start, end);
                return activities;
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to get Activity(ies): {e.Message}", e);
                throw;
            }
        }

        /// <summary>
        /// Get number of minutes of activity from start to end date
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        /// <exception cref="NullReferenceException"></exception>
        [HttpGet("[action]")]
        public async Task<int> GetActivityMinutes(DateOnly start, DateOnly end){
            try{
                if (User.Identity?.Name is null) { throw new NullReferenceException("username/key was null"); }
                return await _activityService.GetActivityMinutesAsync(User.Identity.Name, start, end);
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to get activity minutes): {e.Message}", e);
                throw;
            }
        }

        /// <summary>
        /// Get ratio of activity types from start to end date
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        /// <exception cref="NullReferenceException"></exception>
        [HttpGet("[action]")]
        public async Task<ActivityTypeRatioDto> GetActivityTypeRatio(DateOnly start, DateOnly end){
            try{
                if (User.Identity?.Name is null) { throw new NullReferenceException("username/key was null"); }
                return await _activityService.GetActivityTypeRatioAsync(User.Identity.Name, start, end);
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to get activity ratio): {e.Message}", e);
                throw;
            }
        }

        /// <summary>
        /// Get the number of sets per each muscle group over the course of start to end data
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        [HttpGet("[action]")]
        public async Task<MuscleSetsDto> GetMuscleSets(DateOnly start, DateOnly end){
            try{
                if (User.Identity?.Name is null) { throw new NullReferenceException("username/key was null"); }
                return await _activityService.GetMuscleSetsAsync(User.Identity.Name, start, end);
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to get muscle sets): {e.Message}", e);
                throw;
            }
        }
    }
}