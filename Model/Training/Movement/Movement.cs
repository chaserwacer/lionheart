using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using lionheart.Model.Training.SetEntry;

namespace lionheart.Model.Training
{
    /// <summary>
    /// Representation of a movement performed during a <see cref="TrainingSession"/>.
    /// Contains references to objects that define the movement, and define performed occurrences (sets) of the movement.
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

        /// <summary>
        /// Definition data for the movement being performed.
        /// </summary>
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
        /// <summary>
        /// Movement ordering within the <see cref="TrainingSession"/>.
        /// </summary>
        public required int Ordering { get; set; }

        public MovementDTO ToDTO()
        {
            return new MovementDTO(
                MovementID: MovementID,
                TrainingSessionID: TrainingSessionID,
                MovementDataID: MovementDataID,
                MovementData: MovementData.ToDTO(),
                LiftSets: LiftSets.Select(s => s.ToDTO()).ToList(),
                DistanceTimeSets: DistanceTimeSets.Select(s => s.ToDTO()).ToList(),
                Notes: Notes,
                IsCompleted: IsCompleted,
                Ordering: Ordering
            );
        }

    }

    public record CreateMovementRequest(
        [Required] Guid TrainingSessionID,
        [Required] CreateMovementDataRequest MovementData,
        [Required(AllowEmptyStrings = true)] string Notes
    );

    public record UpdateMovementRequest(
        [Required] Guid MovementID,
        [Required] CreateMovementDataRequest MovementData,
        [Required(AllowEmptyStrings = true)] string Notes,
        [Required] bool IsCompleted
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

    public class MovementOrderUpdate
    {
        public Guid MovementID { get; set; }
        public int Ordering { get; set; }
    }

    public class UpdateMovementOrderRequest
    {
        public Guid TrainingSessionID { get; set; }
        public List<MovementOrderUpdate> Movements { get; set; } = new();
    }
}
