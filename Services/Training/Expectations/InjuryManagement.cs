using lionheart.Model.Analysis;
using lionheart.Model.Training.Expectations;
using lionheart.Model.Training.Patterns;
using lionheart.Services.Training.Balance;
using lionheart.Services.Training.Load;

namespace lionheart.Services.Training.Expectations;

/// <summary>An injury as the expectations read it, active or resolved.</summary>
/// <param name="Families">The families the athlete named on the injury.</param>
/// <param name="Movements">Every movement its check-ins name, with the day each was first named.</param>
/// <param name="LastPain">The newest check-in's pain, 0–10, when there is one.</param>
public sealed record InjuryRecord(
    Guid InjuryId,
    string Name,
    DateOnly Since,
    bool IsActive,
    IReadOnlyList<PatternFamily> Families,
    IReadOnlyList<NamedMovement> Movements,
    int? LastPain);

/// <summary>A movement a check-in named, and the day it was first named — before which it says nothing.</summary>
public sealed record NamedMovement(Guid MovementDataId, DateOnly Named);

/// <summary>Which injuries a day manages around, and what led up to each.</summary>
/// <remarks>
/// Pure. An injury is managed on a day when it is active and dated on or before it. Only "active"
/// is stored, not when an injury resolved, so a resolved injury manages nothing even in the weeks
/// it was active: the past is read as it stands now. Families come from what the athlete named and
/// from the movements his check-ins had named by the day — a squat named in a check-in pauses legs
/// from then on — and only push, pull and legs, the families a target is read for.
/// </remarks>
public static class InjuryManagement
{
    /// <summary>The weeks before an injury its lead-up is read over: the last four against the eight before them.</summary>
    public const int RecentWeeks = 4;
    public const int EarlierWeeks = 8;

    public static IReadOnlyList<ManagedInjury> Managed(
        IReadOnlyList<InjuryRecord> injuries, TrainingLog log, HardSetCounts sets, DateOnly day) =>
    [
        .. injuries
            .Where(injury => injury.IsActive && injury.Since <= day)
            .OrderBy(injury => injury.Since)
            .Select(injury =>
            {
                IReadOnlyList<Guid> movements = [.. injury.Movements.Where(named => named.Named <= day).Select(named => named.MovementDataId)];
                var families = FamiliesOf(injury.Families, movements, log);
                return new ManagedInjury(
                    injury.InjuryId,
                    injury.Name,
                    injury.Since,
                    families,
                    movements,
                    injury.LastPain,
                    [.. families.Select(family => Trend(sets, family, injury.Since))]);
            }),
    ];

    private static IReadOnlyList<PatternFamily> FamiliesOf(
        IReadOnlyList<PatternFamily> named, IReadOnlyList<Guid> movements, TrainingLog log) =>
    [
        .. named
            .Concat(movements
                .Select(log.ClassificationOf)
                .OfType<ClassificationStanding.Classified>()
                .Select(classified => classified.Classification.Family))
            .Where(MovementPatterns.Judged.Contains)
            .Distinct()
            .Order(),
    ];

    /// <summary>The family's weekly hard sets in the four weeks before the day against the eight before those.</summary>
    public static FamilyTrend Trend(HardSetCounts sets, PatternFamily family, DateOnly day)
    {
        var weeks = Enumerable.Range(1, RecentWeeks + EarlierWeeks)
            .Select(back => sets.Of(family, new DayRange(day.AddDays(-7 * back), day.AddDays(-7 * back + 6))))
            .ToList();
        return new FamilyTrend(family, weeks.Take(RecentWeeks).Average(), weeks.Skip(RecentWeeks).Average());
    }
}
