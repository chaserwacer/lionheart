using DKNet.EfCore.DtoGenerator;
using lionheart.Model.DTOs;
using lionheart.WellBeing;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace lionheart.Model.TrainingProgram;

/// <summary>
/// Class to represent a training program.
/// It consists of multiple <see cref="Block"/>s, each containing <see cref="TrainingSession"/>s.
/// </summary>
public class TrainingProgram
{
    [Key]
    public Guid TrainingProgramID { get; init; }

    public Guid UserID { get; init; }

    public string Title { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }
    public DateOnly NextTrainingSessionDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsCompleted { get; set; } = false;
    public List<TrainingSession> TrainingSessions { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    [GenerateDto(typeof(TrainingProgram),
                 Exclude = new[] { "UserID" })]
    public partial class TrainingProgramDTO;

    [GenerateDto(typeof(TrainingProgram),
                 Include = new[] { "TrainingProgramID", "Title", "StartDate", "EndDate", "Tags" })]
    public partial class CreateTrainingProgramRequest;
    [GenerateDto(typeof(TrainingProgram),
    Exclude = new[] { "UserID", "TrainingSessions", "NextTrainingSessionDate"})]
    public partial class UpdateTrainingProgramRequest;

}
