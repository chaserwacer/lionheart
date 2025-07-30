using lionheart.Model.DTOs;
using lionheart.Model.Injury;

namespace lionheart.Model.Mappers;

public static class InjuryMappingExtensions
{
    public static InjuryDTO ToDTO(this Injury.Injury injury)
    {
        return new InjuryDTO
        {
            InjuryID = injury.InjuryID,
            Category = injury.Category,
            InjuryDate = injury.InjuryDate,
            IsResolved = injury.IsResolved,
            InjuryEvents = injury.InjuryEvents.Select(e => new InjuryEventDTO
            {
                TrainingSessionID = e.TrainingSessionID,
                Notes = e.Notes,
                PainLevel = e.PainLevel,
                InjuryType = e.InjuryType,
                CreationTime = e.CreationTime
            }).ToList()
        };
    }
}
