
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
    public Guid? TrainingSessionID { get; set; }

    [Required]
    public required string Notes { get; set; } = string.Empty;
    [Required]
    public required int PainLevel { get; set; }
    [Required]
    public required InjuryEventType InjuryType { get; set; }
    [Required]
    public required DateTime CreationTime { get; set; }
}
public class CreateInjuryRequest
{
    [Required]
    public string Category { get; set; } = string.Empty;

    [Required]
    public DateOnly InjuryDate { get; set; }
}

    public class CreateInjuryEventRequest
    {
    [Required]
    public Guid TrainingSessionID { get; set; }

    public string Notes { get; set; } = string.Empty;

    [Range(0, 10)]
    public int PainLevel { get; set; }

    public InjuryEventType InjuryType { get; set; } = InjuryEventType.flareup;
    }

    public class AddInjuryEventWrapper
{
    [Required]
    public Guid InjuryId { get; set; }

    [Required]
    public required CreateInjuryEventRequest Request { get; set; }
}
