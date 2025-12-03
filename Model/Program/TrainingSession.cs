using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DKNet.EfCore.DtoGenerator;
using lionheart.Model.DTOs;

namespace lionheart.Model.TrainingProgram;

/// <summary>
/// Class to represent a training session within a <see cref="TrainingProgram"/>.
/// A training session consists of multiple <see cref="Movement"/>s.
/// </summary>
public class TrainingSession
{
    [Key]
    public required Guid TrainingSessionID { get; init; }
    [ForeignKey(nameof(TrainingProgram))]
    public required Guid TrainingProgramID { get; init; }
    public TrainingProgram TrainingProgram { get; init; } = null!;
    public required DateOnly Date { get; set; }
    public required TrainingSessionStatus Status { get; set; } = TrainingSessionStatus.Planned;
    public required List<Movement> Movements { get; set; } = [];
    public required DateTime CreationTime { get; set; }
    public required string Notes { get; set; } = string.Empty;


}
public record TrainingSessionDTO(
    Guid TrainingSessionID,
    Guid TrainingProgramID,
    DateOnly Date,
    TrainingSessionStatus Status,
    List<Movement> Movements,
    DateTime CreationTime,
    string Notes
);
public record CreateTrainingSessionRequest(
    DateOnly Date,
    Guid TrainingProgramID,
    string Notes
);
public record UpdateTrainingSessionRequest(
    Guid TrainingSessionID,
    Guid TrainingProgramID,
    DateOnly Date,
    TrainingSessionStatus Status,
    DateTime CreationTime,
    string Notes
);

public enum TrainingSessionStatus
{
    Planned,
    InProgress,
    Completed,
    Skipped,
    AIModified
}