using System.ComponentModel.DataAnnotations;
using lionheart.Model.Training;
using Microsoft.EntityFrameworkCore;

namespace lionheart.ActivityTracking
{
    public class Activity
    {
        // Base Data
        public Guid ActivityID { get; init; }
        public Guid UserID { get; init; }
        public DateTime DateTime { get; set; }
        public int TimeInMinutes { get; set; }
        public int CaloriesBurned { get; set; }
        public string Name { get; set; } = string.Empty;
        public string UserSummary { get; set; } = string.Empty;
        public ActivityPerceivedEffortRatings? PerceivedEffortRatings { get; set; }
    }
    public class ActivityPerceivedEffortRatings
    {
        [Key]
        public required Guid ActivityID { get; init; }
        public required PerceivedEffortRatings PerceivedEffortRatings { get; set; }
    }

    public record ActivityDTO(
        Guid ActivityID,
        Guid UserID,
        DateTime DateTime,
        int TimeInMinutes,
        int CaloriesBurned,
        string Name,
        string UserSummary,
        ActivityPerceivedEffortRatings? PerceivedEffortRatings
    );
    public record CreateActivityRequest(
        Guid UserID,
        DateTime DateTime,
        int TimeInMinutes,
        int CaloriesBurned,
        string Name,
        string UserSummary,
        ActivityPerceivedEffortRatings? PerceivedEffortRatings
    );
    public record UpdateActivityRequest(
        Guid ActivityID,
        Guid UserID,
        DateTime DateTime,
        int TimeInMinutes,
        int CaloriesBurned,
        string Name,
        string UserSummary,
        ActivityPerceivedEffortRatings? PerceivedEffortRatings
    );
}
