using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace lionheart.Model.Program;

/// <summary>
/// Class to represent a training session within a <see cref="Block"/>.
/// A training session consists of multiple <see cref="Movement"/>s.
/// </summary>
public class TrainingSession
{
    [Key]
    public Guid SessionID { get; init; }

    public Guid BlockID { get; init; }
    
    // Will be inside the session DTO for use in the frontend. It can be manually calculated during retreival
    //public int SessionNumber { get; set; }
    public DateOnly Date { get; set; }
    public enum Status
    {
        Planned,
        InProgress,
        Completed,
        Skipped,
    }
    public List<Movement> Movements { get; set; } = [];


}