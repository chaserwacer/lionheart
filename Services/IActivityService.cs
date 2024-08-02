using lionheart.Controllers;
using lionheart.WellBeing;
using lionheart.ActivityTracking;

namespace lionheart.Services
{
    public interface IActivityService
    {
        Task<List<Activity>> GetActivitiesAsync(string userID, DateOnly start, DateOnly end);
        Task<Activity> AddActivityAsync(string userID, CreateActivityRequest activityRequest);
        Task<Activity> AddRunWalkActivityAsync(string userID, CreateRunWalkRequest activityRequest);
        Task<Activity> AddRideActivityAsync(string userID, CreateRideRequest activityRequest);
        Task<Activity> AddLiftActivityAsync(string userID, CreateLiftRequest activityRequest);
    }

    public record CreateActivityRequest(DateTime DateTime, string TimeSpan, int CaloriesBurned, string Name, string UserSummary, int AccumulatedFatigue, int DifficultyRating, int EngagementRating, int ExternalVariablesRating);
    public record CreateRunWalkRequest(DateTime DateTime, string TimeSpan, int CaloriesBurned, string Name, string UserSummary, int AccumulatedFatigue, int DifficultyRating, int EngagementRating, int ExternalVariablesRating,
        double Distance, int ElevationGain, string AveragePace, List<string> MileSplits, string RunType) 
        : CreateActivityRequest(DateTime, TimeSpan, CaloriesBurned, Name, UserSummary, AccumulatedFatigue, DifficultyRating, EngagementRating, ExternalVariablesRating);
    
    public record CreateRideRequest(DateTime DateTime, string TimeSpan, int CaloriesBurned, string Name, string UserSummary, int AccumulatedFatigue, int DifficultyRating, int EngagementRating, int ExternalVariablesRating, 
        double Distance, int ElevationGain, int AveragePower, double AverageSpeed, string RideType) 
        : CreateActivityRequest(DateTime, TimeSpan, CaloriesBurned, Name, UserSummary, AccumulatedFatigue, DifficultyRating, EngagementRating, ExternalVariablesRating); 

    public record CreateLiftRequest(DateTime DateTime, string TimeSpan, int CaloriesBurned, string Name, string UserSummary, int AccumulatedFatigue, int DifficultyRating, int EngagementRating, int ExternalVariablesRating, 
        int Tonnage, string LiftType, string LiftFocus) 
        : CreateActivityRequest(DateTime, TimeSpan, CaloriesBurned, Name, UserSummary, AccumulatedFatigue, DifficultyRating, EngagementRating, ExternalVariablesRating); 
}   