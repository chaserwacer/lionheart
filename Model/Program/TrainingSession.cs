using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using lionheart.Model.DTOs;

namespace lionheart.Model.TrainingProgram;

/// <summary>
/// Class to represent a training session within a <see cref="TrainingProgram"/>.
/// A training session consists of multiple <see cref="Movement"/>s.
/// </summary>
public class TrainingSession
{
    [Key]
    public Guid TrainingSessionID { get; init; }
    [ForeignKey(nameof(TrainingProgram))]
    public Guid TrainingProgramID { get; init; }
    public TrainingProgram TrainingProgram { get; init; } = null!;
    public DateOnly Date { get; set; }
    public TrainingSessionStatus Status { get; set; } = TrainingSessionStatus.Planned;
    public List<Movement> Movements { get; set; } = [];
    public DateTime CreationTime { get; set; }

    public TrainingSessionDTO ToDTO(double sessionNumber)
    {
        return new TrainingSessionDTO
        {
            TrainingSessionID = TrainingSessionID,
            Date = Date,
            Status = Status,
            Movements = Movements.Select(m => m.ToDTO()).ToList(),
            SessionNumber = sessionNumber,
            TrainingProgramID = TrainingProgramID
        };
    }
}
public enum TrainingSessionStatus
    {
        Planned,
        InProgress,
        Completed,
        Skipped,
    }