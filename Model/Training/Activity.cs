using System.ComponentModel.DataAnnotations;
using lionheart.Model.Training;
using Microsoft.EntityFrameworkCore;

namespace lionheart.ActivityTracking
{
    /// <summary>
    /// Representation of some activity performed by a user.
    /// </summary>
    /// <remarks>
    /// Useful as a complement to <see cref="TrainingSession"/>s, this allows the tracking of general activities with less structure.
    /// This enables the tracking of less conventional acitivites [ex: yardwork, concert] that still contribute to health, fatigue, etc.
    /// </remarks>
    public class Activity
    {
        public Guid ActivityID { get; init; }
        public Guid UserID { get; init; }
        public DateTime DateTime { get; set; }
        public int TimeInMinutes { get; set; }
        public int CaloriesBurned { get; set; }
        public string Name { get; set; } = string.Empty;
        public string UserSummary { get; set; } = string.Empty;
        public PerceivedEffortRatings? PerceivedEffortRatings { get; set; }
    }


    public record ActivityDTO(
        Guid ActivityID,
        Guid UserID,
        DateTime DateTime,
        int TimeInMinutes,
        int CaloriesBurned,
        string Name,
        string UserSummary,
        PerceivedEffortRatings? PerceivedEffortRatings
    );
    public record CreateActivityRequest(
        Guid UserID,
        DateTime DateTime,
        int TimeInMinutes,
        int CaloriesBurned,
        string Name,
        string UserSummary,
        PerceivedEffortRatings? PerceivedEffortRatings
    );
    public record UpdateActivityRequest(
        Guid ActivityID,
        Guid UserID,
        DateTime DateTime,
        int TimeInMinutes,
        int CaloriesBurned,
        string Name,
        string UserSummary,
        PerceivedEffortRatings? PerceivedEffortRatings
    );
}
