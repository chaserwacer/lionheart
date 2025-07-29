using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace lionheart.Model.Injury;

public class Injury
{
    [Key]
    public Guid InjuryID { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserID { get; init; }

    [Required]
    public string Category { get; set; } = string.Empty;

    [Required]
    public DateOnly InjuryDate { get; set; }

    public bool IsResolved { get; set; } = false;

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
    public Guid TrainingSessionID { get; init; }

    public string Notes { get; set; } = string.Empty;

    public int PainLevel { get; set; }

    public InjuryEventType InjuryType { get; set; } = InjuryEventType.flareup;

    public DateTime CreationTime { get; set; } = DateTime.UtcNow;
}


public enum InjuryEventType
{
    flareup,
    treatment,
    injury
}
