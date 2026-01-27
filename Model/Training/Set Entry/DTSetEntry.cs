using System.ComponentModel.DataAnnotations;

namespace lionheart.Model.Training.SetEntry
{
    /// <summary>
    /// Represents an occurence of a <see cref="Movement"/> being performed in some distance/time based manner.
    /// </summary>
    /// <remarks>
    /// TODO: Update class to encapsulate metrics into recommended vs actual groupings.
    /// </remarks>
    public class DTSetEntry
    {
        public required Guid SetEntryID { get; init; }
        public required Guid MovementID { get; init; }
        public Movement Movement { get; set; } = null!;
        public required double RecommendedDistance { get; set; }
        public required double ActualDistance { get; set; }

        public required TimeSpan IntervalDuration { get; set; }
        public required TimeSpan TargetPace { get; set; }
        public required TimeSpan ActualPace { get; set; }

        public required TimeSpan RecommendedDuration { get; set; }
        public required TimeSpan ActualDuration { get; set; }

        public required TimeSpan RecommendedRest { get; set; }
        public required TimeSpan ActualRest { get; set; }
        public required IntervalType IntervalType { get; set; }
        public required DistanceUnit DistanceUnit { get; set; }
        public required double ActualRPE { get; set; }
        public DTSetEntryDTO ToDTO()
        {
            return new DTSetEntryDTO(
                SetEntryID: SetEntryID,
                MovementID: MovementID,
                RecommendedDistance: RecommendedDistance,
                ActualDistance: ActualDistance,
                IntervalDuration: IntervalDuration,
                TargetPace: TargetPace,
                ActualPace: ActualPace,
                RecommendedDuration: RecommendedDuration,
                ActualDuration: ActualDuration,
                RecommendedRest: RecommendedRest,
                ActualRest: ActualRest,
                IntervalType: IntervalType,
                DistanceUnit: DistanceUnit,
                ActualRPE: ActualRPE
            );
        }
  
    }

    public record DTSetEntryDTO(
        Guid SetEntryID,
        Guid MovementID,
        double RecommendedDistance,
        double ActualDistance,
        TimeSpan IntervalDuration,
        TimeSpan TargetPace,
        TimeSpan ActualPace,
        TimeSpan RecommendedDuration,
        TimeSpan ActualDuration,
        TimeSpan RecommendedRest,
        TimeSpan ActualRest,
        IntervalType IntervalType,
        DistanceUnit DistanceUnit,
        double ActualRPE
    );

    public record CreateDTSetEntryRequest(
        [Required]Guid MovementID,
        double RecommendedDistance,
        [Required]double ActualDistance,
        [Required]TimeSpan IntervalDuration,
        TimeSpan TargetPace,
        [Required]TimeSpan ActualPace,
        TimeSpan RecommendedDuration,
        [Required]TimeSpan ActualDuration,
        TimeSpan RecommendedRest,
        [Required]TimeSpan ActualRest,
        [Required]IntervalType IntervalType,
        [Required]DistanceUnit DistanceUnit,
        [Required]double ActualRPE
    );

    public record UpdateDTSetEntryRequest(
        [Required]Guid SetEntryID,
        [Required]Guid MovementID,
        double RecommendedDistance,
        [Required]double ActualDistance,
        [Required]TimeSpan IntervalDuration,
        TimeSpan TargetPace,
        [Required]TimeSpan ActualPace,
        TimeSpan RecommendedDuration,
        [Required]TimeSpan ActualDuration,
        TimeSpan RecommendedRest,
        [Required]TimeSpan ActualRest,
        [Required]IntervalType IntervalType,
        [Required]DistanceUnit DistanceUnit,
        [Required]double ActualRPE
    );

    public enum IntervalType
    {
        // ────────────── Continuous Work (no rest tracked) ──────────────
        ContinuousDistance,       // e.g., "5km easy run" – distance only
        ContinuousTime,           // e.g., "30:00 tempo run" – time only
        ContinuousDistanceAndTime,// e.g., "1000m swim @ 1:45/100" – both distance & time targets

        // ────────────── Fixed Rest Repeats ──────────────
        RepetitionDistance,       // e.g., "10×200m w/30s rest" – distance only
        RepetitionTime,           // e.g., "8×2:00 @Z3 w/1:00 rest" – time only
        RepetitionDistanceAndTime,// e.g., "6×400m @1:40 w/1:00 rest" – both distance & time targets

        // ────────────── Send-Off / On-the-Clock Sets ──────────────
        IntervalDistance,          // e.g., "10×50m ON :50" – distance only
        IntervalTime,              // e.g., "8×1:00 ON 1:30" – time only
        IntervalDistanceAndTime    // e.g., "5×100m @1:20 ON 2:00" – both distance & time targets
    }

    public enum DistanceUnit
    {
        Meters,
        Yards,
        Miles,
        Kilometers
    }
}