
using System.ComponentModel.DataAnnotations;
using lionheart.Model.Injury;
public class UpdateInjuryRequest
{
    [Required]
    public required Guid InjuryID { get; set; }
    [Required]
    public required string Name { get; set; } = string.Empty;
    [Required]
    public required string Notes { get; set; } = string.Empty;

    [Required]
    public required bool IsActive { get; set; }

}
public class InjuryDTO
{
    [Required]
    public required Guid InjuryID { get; set; }
    [Required]
    public required string Name { get; set; } = string.Empty;
    [Required]
    public required string Notes { get; set; } = string.Empty;
    [Required]
    public required DateOnly InjuryDate { get; set; }
    [Required]
    public required bool IsActive { get; set; }
    [Required]
    public required List<InjuryEventDTO> InjuryEvents { get; set; } = new();
}

public class InjuryEventDTO
{
    [Required]
    public required Guid InjuryEventID { get; set; }
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
    public string Name { get; set; } = string.Empty;

    [Required]
    public DateOnly InjuryDate { get; set; }
    [Required(AllowEmptyStrings = true)]
    public string Notes { get; set; } = string.Empty;
}

public class CreateInjuryEventRequest
{
    public Guid? TrainingSessionID { get; set; }
    [Required]
    public required Guid InjuryID { get; set; }
    [Required(AllowEmptyStrings = true)]
    public required string Notes { get; set; } = string.Empty;

    [Required][Range(0, 10)]
    public required int PainLevel { get; set; }
     [Required]
    public required InjuryEventType InjuryType { get; set; }
}

public class UpdateInjuryEventRequest
{
    public Guid? TrainingSessionID { get; set; }
    [Required]
    public Guid InjuryEventID { get; set; }

    [Required]
    [Range(0, 10)]
    public int PainLevel { get; set; }

    [Required]
    public InjuryEventType InjuryType { get; set; }

    [Required(AllowEmptyStrings = true)]
    public string Notes { get; set; } = string.Empty;
}

