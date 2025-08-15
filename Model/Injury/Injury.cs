using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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

    public InjuryDTO ToDTO()
    {
        return new InjuryDTO
        {
            InjuryID = InjuryID,
            Name = Name,
            Notes = Notes,
            InjuryDate = InjuryDate,
            IsActive = IsActive,
            // Sort injury events by most recent CreationTime first
            InjuryEvents = InjuryEvents
                .OrderByDescending(ie => ie.CreationTime)
                .Select(ie => new InjuryEventDTO
            {
                InjuryEventID = ie.InjuryEventID,
                TrainingSessionID =  ie.TrainingSessionID.Equals(Guid.Empty) ? null : ie.TrainingSessionID,
                Notes = ie.Notes,
                PainLevel = ie.PainLevel,
                InjuryType = ie.InjuryType,
                CreationTime = ie.CreationTime
            }).ToList()
        };
    }
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

    public InjuryEventType InjuryType { get; set; } = InjuryEventType.checkin;

    public DateTime CreationTime { get; set; } = DateTime.UtcNow;
}


public enum InjuryEventType
{
    treatment,
    checkin
}
