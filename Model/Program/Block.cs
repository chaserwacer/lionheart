using System.ComponentModel.DataAnnotations;
using lionheart.Model.Program;

/// <summary>
/// Represents a block of training within a <see cref="Program"/>.
/// A block contains multiple <see cref="TrainingSession"/>
/// </summary>
public class Block
{
    public Guid ProgramID { get; init; }
    [Key]
    public Guid BlockID { get; init; }
    public string Title { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }
    public List<TrainingSession> Sessions { get; set; } = [];
}