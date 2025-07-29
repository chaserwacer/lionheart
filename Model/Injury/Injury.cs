using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace lionheart.Model.Injury;

public class Injury
{
    public Guid UserID { get; init; }
    public string Category { get; set; } = string.Empty;
    public DateOnly InjuryDate { get; set; }
    public bool IsResolved { get; set; } = false;
    public List<InjuryEvent> injuryEvents { get; set; } = [];
}

public class InjuryEvent
{
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
