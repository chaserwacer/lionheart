using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using lionheart.Model.Training.SetEntry;

namespace lionheart.Model.Training
{
    /// <summary>
    /// Represents a thing that a user does in a training session.
    /// The movement performed is defined by <see cref="MovementData"/>
    /// </summary>

    public class Movement
    {
        [Key]
        public required Guid MovementID { get; init; }

        [ForeignKey("TrainingSession")]
        public required Guid TrainingSessionID { get; init; }
        public TrainingSession TrainingSession { get; set; } = null!;

        [Required]
        public required Guid MovementDataID { get; set; }

        /// <remarks>
        /// <see cref="MovementData"/> is referenced by FK.
        /// If a matching MovementData doesn't exist, it should be created first, then referenced.
        /// </remarks>
        [ForeignKey("MovementDataID")]
        public MovementData MovementData { get; set; } = null!;

        public required List<LiftSetEntry> LiftSets { get; set; }
        public required List<DTSetEntry> DistanceTimeSets { get; set; }
        public required string Notes { get; set; } = string.Empty;
        public required bool IsCompleted { get; set; } = false;
        public required int Ordering { get; set; }
    }

    public record CreateMovementRequest(
        Guid TrainingSessionID,
        CreateMovementDataRequest MovementData,
        string Notes
    );
    public record UpdateMovementRequest(
        Guid MovementID,
        CreateMovementDataRequest MovementData,
        string Notes,
        bool IsCompleted
    );
    public record MovementDTO(
        Guid MovementID,
        Guid TrainingSessionID,
        Guid MovementDataID,
        MovementDataDTO MovementData,
        List<LiftSetEntryDTO> LiftSets,
        List<DTSetEntryDTO> DistanceTimeSets,
        string Notes,
        bool IsCompleted,
        int Ordering
    );
}
