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
        Task<int> GetActivityMinutesAsync(string userID, DateOnly start, DateOnly end);
        Task<ActivityTypeRatioDto> GetActivityTypeRatioAsync(string userID, DateOnly start, DateOnly end);
        Task<WeeklyMuscleSetsDto> GetWeeklyMuscleSetsAsync(string userID, DateOnly start, DateOnly end);
    }

    public record ActivityTypeRatioDto(int NumberLifts, int NumberRunWalks, int NumberRides);
    public record CreateActivityRequest(DateTime DateTime, int TimeInMinutes, int CaloriesBurned, string Name, string UserSummary, int AccumulatedFatigue, int DifficultyRating, int EngagementRating, int ExternalVariablesRating);
    public record CreateRunWalkRequest(DateTime DateTime, int TimeInMinutes, int CaloriesBurned, string Name, string UserSummary, int AccumulatedFatigue, int DifficultyRating, int EngagementRating, int ExternalVariablesRating,
        double Distance, int ElevationGain, int AveragePaceInSeconds, List<int> MileSplitsInSeconds, string RunType) 
        : CreateActivityRequest(DateTime, TimeInMinutes, CaloriesBurned, Name, UserSummary, AccumulatedFatigue, DifficultyRating, EngagementRating, ExternalVariablesRating);
    
    public record CreateRideRequest(DateTime DateTime, int TimeInMinutes, int CaloriesBurned, string Name, string UserSummary, int AccumulatedFatigue, int DifficultyRating, int EngagementRating, int ExternalVariablesRating, 
        double Distance, int ElevationGain, int AveragePower, double AverageSpeed, string RideType) 
        : CreateActivityRequest(DateTime, TimeInMinutes, CaloriesBurned, Name, UserSummary, AccumulatedFatigue, DifficultyRating, EngagementRating, ExternalVariablesRating); 

    public record CreateLiftRequest(DateTime DateTime, int TimeInMinutes, int CaloriesBurned, string Name, string UserSummary, int AccumulatedFatigue, int DifficultyRating, int EngagementRating, int ExternalVariablesRating, 
        int Tonnage, string LiftType, string LiftFocus, int QuadSets, int HamstringSets, int BicepSets, int TricepSets, int ShoulderSets, int BackSets, int ChestSets) 
        : CreateActivityRequest(DateTime, TimeInMinutes, CaloriesBurned, Name, UserSummary, AccumulatedFatigue, DifficultyRating, EngagementRating, ExternalVariablesRating); 
    
    public record WeeklyMuscleSetsDto(int QuadSets, int HamstringSets, int BicepSets, int TricepSets, int ShoulderSets, int ChestSets, int BackSets);
}   