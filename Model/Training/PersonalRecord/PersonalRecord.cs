using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using lionheart.Model.Training.SetEntry;

namespace lionheart.Model.Training;

/// <summary>
/// Represents a Personal Record (PR) for a user.
/// A PR is tracked for each unique <see cref="MovementData"/>.
/// </summary>
public class PersonalRecord
{
    [Key]
    [Required]
    public required Guid PersonalRecordID { get; init; }

    [Required]
    public required Guid UserID { get; init; }

    [Required]
    public required Guid MovementDataID { get; set; }

    [ForeignKey("MovementDataID")]
    public required MovementData MovementData { get; set; }

    [Required]
    public required PersonalRecordType PRType { get; init; }
    
    [Required]
    public required double Weight { get; set; }
    [Required]
    public required WeightUnit WeightUnit { get; set; }
    [Required]
    public required int Reps { get; set; }
    public double Volume => Weight * Reps;

    [Required]
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the previous PR was achieved (if any).
    /// This helps track how long it took to beat the previous PR.
    /// </summary>
    public DateTime? PreviousPRCreatedAt { get; set; }

    /// <summary>
    /// Reference to the previous PR that was beaten.
    /// Used for reverting if a set is decreased.
    /// </summary>
    public Guid? PreviousPersonalRecordID { get; set; }

    [ForeignKey("PreviousPersonalRecordID")]
    public PersonalRecord? PreviousPersonalRecord { get; set; }

    /// <summary>
    /// The LiftSetEntry that achieved this PR (if tracked from a training session).
    /// </summary>
    public Guid? SourceLiftSetEntryID { get; set; }

    [ForeignKey("SourceLiftSetEntryID")]
    public LiftSetEntry? SourceLiftSetEntry { get; set; }

    /// <summary>
    /// Whether this PR is currently active (the current record).
    /// When a new PR is set, the old one becomes inactive.
    /// </summary>
    [Required]
    public required bool IsActive { get; set; } = true;
}


public enum PersonalRecordType
{
    /// <summary>
    /// Maximum weight lifted (1RM or any rep count, comparing weight only).
    /// </summary>
    Strength,

    /// <summary>
    /// Maximum volume (weight * reps).
    /// </summary>
    Volume
}

public record PersonalRecordDTO(
    Guid PersonalRecordID,
    Guid UserID,
    Guid MovementDataID,
    MovementDataDTO MovementData,
    PersonalRecordType PRType,
    double Weight,
    WeightUnit WeightUnit,
    int Reps,
    double Volume,
    DateTime CreatedAt,
    DateTime? PreviousPRCreatedAt,
    Guid? PreviousPersonalRecordID,
    Guid? SourceLiftSetEntryID,
    bool IsActive
);

public record MovementDataPRSummary(
    Guid MovementDataID,
    MovementDataDTO MovementData,
    PersonalRecordDTO? StrengthPR,
    PersonalRecordDTO? VolumePR,
    DateTime? LastPRDate
);

public record PRAttemptResult(
    bool IsNewStrengthPR,
    bool IsNewVolumePR,
    PersonalRecordDTO? NewStrengthPR,
    PersonalRecordDTO? NewVolumePR,
    PersonalRecordDTO? PreviousStrengthPR,
    PersonalRecordDTO? PreviousVolumePR
);
