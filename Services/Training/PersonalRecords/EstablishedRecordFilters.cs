using lionheart.Model.Training.PersonalRecords;

namespace lionheart.Services.Training.PersonalRecords;

/// <summary>
/// The established read's filters, each one passing every record through when its half of the ask
/// is absent.
/// </summary>
/// <remarks>
/// All three reach SQL. A subject compares as its one converted column, with the converter applied
/// to the parameter; the window compares the stored instant, which is UTC.
/// </remarks>
internal static class EstablishedRecordFilters
{
    extension(IQueryable<PersonalRecord> records)
    {
        public IQueryable<PersonalRecord> OnMovement(Guid? movementDataId)
        {
            if (movementDataId is not { } id) return records;

            RecordSubject subject = new RecordSubject.Movement(id);
            return records.Where(record => record.Subject == subject);
        }

        public IQueryable<PersonalRecord> AchievedFrom(DateTimeOffset? from)
        {
            if (from is not { } bound) return records;

            var start = bound.UtcDateTime;
            return records.Where(record => record.AchievedAt >= start);
        }

        public IQueryable<PersonalRecord> AchievedUntil(DateTimeOffset? to)
        {
            if (to is not { } bound) return records;

            var end = bound.UtcDateTime;
            return records.Where(record => record.AchievedAt <= end);
        }
    }
}
