namespace lionheart.Model.Strava
{
    /// <summary>
    /// Representation of a single activity imported from the Strava API for a user.
    /// </summary>
    /// <remarks>
    /// This is a subset of the fields returned by Strava's <c>SummaryActivity</c> object, augmented with
    /// the raw JSON payload (<see cref="RawJson"/>) so no information is lost. Stored in the lionheart database
    /// so activities are available without re-querying Strava.
    /// </remarks>
    public class StravaActivity
    {
        public Guid ObjectID { get; init; }
        public Guid UserID { get; init; }

        /// <summary>
        /// Strava's own identifier for the activity. Unique per user; used to upsert on sync.
        /// </summary>
        public long StravaActivityID { get; set; }

        public string Name { get; set; } = string.Empty;
        public string SportType { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime StartDateLocal { get; set; }
        public int ElapsedTimeSeconds { get; set; }
        public int MovingTimeSeconds { get; set; }
        public double DistanceMeters { get; set; }
        public double TotalElevationGainMeters { get; set; }
        public double AverageSpeed { get; set; }
        public double MaxSpeed { get; set; }
        public double? AverageHeartrate { get; set; }
        public double? MaxHeartrate { get; set; }
        public double? Calories { get; set; }

        /// <summary>
        /// Raw JSON for this activity as returned by the Strava API.
        /// </summary>
        public string RawJson { get; set; } = string.Empty;

        /// <summary>
        /// UTC timestamp of the last time this record was synced from Strava.
        /// </summary>
        public DateTime SyncedAt { get; set; }

        public StravaActivityDTO ToDTO()
        {
            return new StravaActivityDTO(
                ObjectID: ObjectID,
                StravaActivityID: StravaActivityID,
                Name: Name,
                SportType: SportType,
                StartDate: StartDate,
                StartDateLocal: StartDateLocal,
                ElapsedTimeSeconds: ElapsedTimeSeconds,
                MovingTimeSeconds: MovingTimeSeconds,
                DistanceMeters: DistanceMeters,
                TotalElevationGainMeters: TotalElevationGainMeters,
                AverageSpeed: AverageSpeed,
                MaxSpeed: MaxSpeed,
                AverageHeartrate: AverageHeartrate,
                MaxHeartrate: MaxHeartrate,
                Calories: Calories
            );
        }
    }

    /// <summary>
    /// Frontend-facing representation of a <see cref="StravaActivity"/> (omits raw JSON and backend-only fields).
    /// </summary>
    public record StravaActivityDTO(
        Guid ObjectID,
        long StravaActivityID,
        string Name,
        string SportType,
        DateTime StartDate,
        DateTime StartDateLocal,
        int ElapsedTimeSeconds,
        int MovingTimeSeconds,
        double DistanceMeters,
        double TotalElevationGainMeters,
        double AverageSpeed,
        double MaxSpeed,
        double? AverageHeartrate,
        double? MaxHeartrate,
        double? Calories
    );
}
