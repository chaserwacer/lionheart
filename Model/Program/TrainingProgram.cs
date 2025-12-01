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
    public required Guid TrainingProgramID { get; init; }

    public required Guid UserID { get; init; }

    public required string Title { get; set; } = string.Empty;

    public required DateOnly StartDate { get; set; }
    public DateOnly NextTrainingSessionDate { get; set; }
    public required DateOnly EndDate { get; set; }
    public required bool IsCompleted { get; set; } = false;
    public required List<TrainingSession> TrainingSessions { get; set; } = [];
    public required List<string> Tags { get; set; } = [];



}
[GenerateDto(typeof(TrainingProgram),
                Exclude = new[] { "UserID" })]
public partial record TrainingProgramDTO;

[GenerateDto(typeof(TrainingProgram),
             Include = new[] { "TrainingProgramID", "Title", "StartDate", "EndDate", "Tags" })]
public partial record CreateTrainingProgramRequest;
[GenerateDto(typeof(TrainingProgram),
Exclude = new[] { "UserID", "TrainingSessions", "NextTrainingSessionDate" })]
public partial record UpdateTrainingProgramRequest;