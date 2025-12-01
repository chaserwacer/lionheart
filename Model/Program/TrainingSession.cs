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
[GenerateDto(typeof(TrainingSession),
                 Exclude = new[] { "TrainingProgram" })]
public partial record TrainingSessionDTO;
[GenerateDto(typeof(TrainingSession),
             Include = new[] { "Date", "TrainingProgramID", "Notes" })]
public partial record CreateTrainingSessionRequest;
[GenerateDto(typeof(TrainingSession), Exclude = new[] { "TrainingProgram", "CreationTime", "Movements" })]
public partial record UpdateTrainingSessionRequest;

public enum TrainingSessionStatus
{
    Planned,
    InProgress,
    Completed,
    Skipped,
    AIModified
}