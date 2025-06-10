using lionheart.WellBeing;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace lionheart.Model.Program;

/// <summary>
/// Class to represent a training program.
/// It consists of multiple <see cref="Block"/>s, each containing <see cref="TrainingSession"/>s.
/// </summary>
public class Program
{
    [Key]
    public Guid ProgramID { get; init; }

    public Guid UserID { get; init; }

    public string Title { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }
    public DateOnly NextTrainingSessionDate { get; set; }
    public DateOnly EndDate { get; set; }
    public List<TrainingSession> TrainingSessions { get; set; } = [];
    /// <summary>
    /// Hold label tags for the program. [Ex: "Powerlifting", "Hypertrophy", "Endurance"]
    /// In the future, this may need to be beefed up and use some sort of more concrete tagging system.
    /// </summary>
    public List<string> Tags { get; set; } = [];
}
