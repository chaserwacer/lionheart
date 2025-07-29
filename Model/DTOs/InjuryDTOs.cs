
using System.ComponentModel.DataAnnotations;
using lionheart.Model.Injury;

public class InjuryDTO
{
    [Required]
    public required Guid InjuryID { get; set; }
    [Required]
    public required string Category { get; set; } = string.Empty;
    [Required]
    public required DateOnly InjuryDate { get; set; }
    [Required]
    public required bool IsResolved { get; set; }
    [Required]
    public required List<InjuryEventDTO> InjuryEvents { get; set; } = new();
}

public class InjuryEventDTO
{
    [Required]
    public required Guid TrainingSessionID { get; set; }
    [Required]
    public required string Notes { get; set; } = string.Empty;
    [Required]
    public required int PainLevel { get; set; }
    [Required]
    public required InjuryEventType InjuryType { get; set; }
    [Required]
    public required DateTime CreationTime { get; set; }
}
