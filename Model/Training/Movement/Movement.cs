using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using lionheart.Model.Training.SetEntry;

namespace lionheart.Model.Training
{
    /// <summary>
    /// Rerepresents a thing that a user does in a training session.
    /// A <see cref="MovementBase"/> is chosen, and a <see cref="MovementData"/> can be applied.
    /// A <see cref="Movement"/> can have multiple <see cref="LiftSetEntry"/>s, which represent the sets performed during the movement.
    /// </summary>
    /// <remarks>
    /// A prexisiting <see cref="MovementBase"/> is referenced, a new entry in the db will not be created.
    /// The <see cref="MovementData"/> will be created and will not attempt to reference an existing one.
    /// </remarks>
    public class Movement
    {
        [Key]
        public required Guid MovementID { get; init; }
        [ForeignKey("TrainingSession")]
        public required Guid TrainingSessionID { get; init; }
        public TrainingSession TrainingSession { get; set; } = null!;
        public required MovementData MovementData { get; set; }
        public required List<LiftSetEntry> LiftSets { get; set; }
        public required List<DTSetEntry> DistanceTimeSets { get; set; }
        public required string Notes { get; set; } = string.Empty;
        public required bool IsCompleted { get; set; } = false;
        public required int Ordering { get; set; }

    }
    public record CreateMovementRequest(
        Guid TrainingSessionID,
        Guid MovementBaseID,
        MovementData MovementData,
        string Notes
    );
    public record UpdateMovementRequest(
        Guid MovementID,
        Guid MovementBaseID,
        MovementData MovementData,
        string Notes,
        bool IsCompleted
    );
    public record MovementDTO(
        Guid MovementID,
        Guid TrainingSessionID,
        Guid MovementBaseID,
        MovementData MovementData,
        List<LiftSetEntryDTO> LiftSets,
        List<DTSetEntryDTO> DistanceTimeSets,
        string Notes,
        bool IsCompleted,
        int Ordering
    );


}

   