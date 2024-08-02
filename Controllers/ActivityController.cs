using System.Diagnostics;
using lionheart;
using lionheart.Services;
using Microsoft.AspNetCore.Mvc;
namespace lionheart.Controllers
{
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

        [HttpPost("[action]")]
        public async Task<IActionResult> AddActivity(CreateActivityRequest activityRequest)
        {
            try
            {
                if (User.Identity?.Name is null) { throw new NullReferenceException("Error adding Wellness State - username/key was null"); }
                var activity = await _activityService.AddActivityAsync(User.Identity.Name, activityRequest);
                return Ok(activity);
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to add Activity: {e.Message}", e);
                throw;
            }
        }
        [HttpPost("[action]")]
        public async Task<IActionResult> AddRunWalkActivity(CreateRunWalkRequest activityRequest)
        {
            try
            {
                if (User.Identity?.Name is null) { throw new NullReferenceException("Error adding Wellness State - username/key was null"); }
                var activity = await _activityService.AddRunWalkActivityAsync(User.Identity.Name, activityRequest);
                return Ok(activity);
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to add Activity: {e.Message}", e);
                throw;
            }
        }
        [HttpPost("[action]")]
        public async Task<IActionResult> AddRideActivity(CreateRideRequest activityRequest)
        {
            try
            {
                if (User.Identity?.Name is null) { throw new NullReferenceException("Error adding Wellness State - username/key was null"); }
                var activity = await _activityService.AddRideActivityAsync(User.Identity.Name, activityRequest);
                return Ok(activity);
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to add Activity: {e.Message}", e);
                throw;
            }
        }
        [HttpPost("[action]")]
        public async Task<IActionResult> AddLiftActivity(CreateLiftRequest activityRequest)
        {
            try
            {
                if (User.Identity?.Name is null) { throw new NullReferenceException("Error adding Wellness State - username/key was null"); }
                var activity = await _activityService.AddLiftActivityAsync(User.Identity.Name, activityRequest);
                return Ok(activity);
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to add Activity: {e.Message}", e);
                throw;
            }
        }

        [HttpGet("[action]")]
        public async Task<List<lionheart.ActivityTracking.Activity>> GetActivities(DateOnly start, DateOnly end){
            try{
                if (User.Identity?.Name is null) { throw new NullReferenceException("Error adding Wellness State - username/key was null"); }
                var activities = await _activityService.GetActivitiesAsync(User.Identity.Name, start, end);
                return activities;
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to get Activity(ies): {e.Message}", e);
                throw;
            }
        }

        [HttpGet("[action]")]
        public async Task<int> GetActivityMinutes(DateOnly start, DateOnly end){
            try{
                if (User.Identity?.Name is null) { throw new NullReferenceException("Error adding Wellness State - username/key was null"); }
                return await _activityService.GetActivityMinutesAsync(User.Identity.Name, start, end);
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to get activity minutes): {e.Message}", e);
                throw;
            }
        }

        [HttpGet("[action]")]
        public async Task<ActivityTypeRatioDto> GetActivityTypeRatio(DateOnly start, DateOnly end){
            try{
                if (User.Identity?.Name is null) { throw new NullReferenceException("Error adding Wellness State - username/key was null"); }
                return await _activityService.GetActivityTypeRatioAsync(User.Identity.Name, start, end);
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to get activity ratio): {e.Message}", e);
                throw;
            }
        }
    }
}