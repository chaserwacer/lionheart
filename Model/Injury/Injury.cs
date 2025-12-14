using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using lionheart.Model.TrainingProgram;
using Parlot.Fluent;

namespace lionheart.Model.Injury;

public class Injury
{
    [Key]
    public Guid InjuryID { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserID { get; init; }

    [Required]
    public string Name { get; set; } = string.Empty;
    [Required]
    public string Notes { get; set; } = string.Empty;

    [Required]
    public DateOnly InjuryDate { get; set; }

    public bool IsActive { get; set; } = false;

    public List<InjuryEvent> InjuryEvents { get; set; } = new();

   
}

public class InjuryEvent
{
    [Key]
    public Guid InjuryEventID { get; set; } = Guid.NewGuid();

    [Required]
    public Guid InjuryID { get; set; }
    public Injury Injury { get; set; } = null!;

    [Required]
    public Guid TrainingSessionID { get; set; }

    public string Notes { get; set; } = string.Empty;

    public int PainLevel { get; set; }
    public List<Guid> MovementIDs { get; set; } = new();
    public InjuryEventType InjuryType { get; set; } = InjuryEventType.checkin;

    public DateTime CreationTime { get; set; } = DateTime.UtcNow;
}


public enum InjuryEventType
{
    treatment,
    checkin
}

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
    public List<Guid> MovementIDs { get; set; } = new();

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
    [Required]
    public required List<Guid> MovementIDs { get; set; } = new();
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
    [Required]
    public required List<Guid> MovementIDs { get; set; } = new();
}


