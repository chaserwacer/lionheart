using lionheart.Model.Training.Balance;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Sets;

namespace lionheart.Services.Training.Balance;

/// <summary>A hard set placed by what its movement trains, on the day it was performed.</summary>
public sealed record PlacedSet(DateOnly Day, LiftSet Set, MovementClassification Trains);

/// <summary>
/// Weekly volume as a function of the log: hard sets counted by family, pattern and muscle, the
/// last day each was trained, and what is up next.
/// </summary>
/// <remarks>Pure, so every counting rule is tested without a database.</remarks>
public static class TrainingVolumeAnalysis
{
    public static TrainingVolume Build(TrainingLog log, DateOnly endingOn, ReadinessNote readiness)
    {
        var window = VolumeTargets.WindowEndingOn(endingOn);
        var history = Placed(log, DateOnly.MinValue, endingOn).ToList();
        var inWindow = history.Where(placed => placed.Day >= window.From).ToList();
        var families = Families(inWindow, history);

        return new TrainingVolume(
            window,
            families,
            [.. Enum.GetValues<MovementPattern>().Select(pattern => Pattern(pattern, inWindow, history))],
            [.. MuscleGroups.All.Select(muscle => Muscle(muscle, inWindow, history))],
            Unplaced(log, window.From, endingOn),
            UpNextOf(families, trainedToday: inWindow.Any(placed => placed.Day == endingOn), readiness));
    }

    /// <summary>Every hard set between two days whose movement is classified, oldest first.</summary>
    public static IEnumerable<PlacedSet> Placed(TrainingLog log, DateOnly from, DateOnly to) =>
        log.Between(from, to).SelectMany(session => session.HardSets.SelectMany(set =>
            log.ClassificationOf(set.MovementDataId) is ClassificationStanding.Classified classified
                ? [new PlacedSet(session.Day, set, classified.Classification)]
                : Array.Empty<PlacedSet>()));

    /// <summary>Push, pull and legs, each set counted once toward its movement's family.</summary>
    public static IReadOnlyList<FamilyVolume> Families(IReadOnlyList<PlacedSet> inWindow, IReadOnlyList<PlacedSet> history) =>
    [
        .. MovementPatterns.Judged.Select(family => new FamilyVolume(
            family,
            inWindow.Count(placed => placed.Trains.Family == family),
            VolumeTargets.Family,
            LastTrained(family, history)))
    ];

    /// <summary>
    /// The last day that trained the family: any compound set of it, or accessory work making up a
    /// good share of the day.
    /// </summary>
    private static DateOnly? LastTrained(PatternFamily family, IReadOnlyList<PlacedSet> history) =>
        history
            .GroupBy(placed => placed.Day)
            .Where(day => Trained(family, [.. day]))
            .Select(day => (DateOnly?)day.Key)
            .DefaultIfEmpty(null)
            .Max();

    private static bool Trained(PatternFamily family, IReadOnlyList<PlacedSet> day)
    {
        var ofFamily = day.Where(placed => placed.Trains.Family == family).ToList();
        return ofFamily.Any(placed => MovementPatterns.Compound.Contains(placed.Trains.Pattern))
               || (ofFamily.Count > 0 && ofFamily.Count >= VolumeTargets.AccessoryShare * day.Count);
    }

    private static PatternVolume Pattern(MovementPattern pattern, IReadOnlyList<PlacedSet> inWindow, IReadOnlyList<PlacedSet> history) =>
        new(
            pattern,
            inWindow.Count(placed => placed.Trains.Pattern == pattern),
            MovementPatterns.Compound.Contains(pattern) ? VolumeTargets.Pattern : null,
            LastDay(history.Where(placed => placed.Trains.Pattern == pattern)));

    private static MuscleVolume Muscle(MuscleGroup muscle, IReadOnlyList<PlacedSet> inWindow, IReadOnlyList<PlacedSet> history) =>
        new(
            muscle,
            inWindow.Count(placed => placed.Trains.Primary.Contains(muscle)),
            inWindow.Count(placed => placed.Trains.Secondary.Contains(muscle)),
            VolumeTargets.Muscle,
            LastDay(history.Where(placed => placed.Trains.Primary.Contains(muscle))));

    private static IReadOnlyList<UnplacedMovement> Unplaced(TrainingLog log, DateOnly from, DateOnly to) =>
    [
        .. log.Between(from, to)
            .SelectMany(session => session.HardSets)
            .Where(set => log.ClassificationOf(set.MovementDataId) is ClassificationStanding.Unclassified)
            .GroupBy(set => set.MovementDataId)
            .Select(group => new UnplacedMovement(group.Key, log.NameOf(group.Key), group.Count()))
            .OrderByDescending(unplaced => unplaced.HardSets)
            .ThenBy(unplaced => unplaced.Name, StringComparer.OrdinalIgnoreCase)
    ];

    /// <summary>
    /// The families trained longest ago — never before any other — named together when they were
    /// last trained on the same day.
    /// </summary>
    public static UpNext UpNextOf(IReadOnlyList<FamilyVolume> families, bool trainedToday, ReadinessNote readiness)
    {
        if (families.All(family => family.LastTrained is null)) return new UpNext.NothingLogged();

        var due = families
            .GroupBy(family => family.LastTrained)
            .OrderBy(group => group.Key ?? DateOnly.MinValue)
            .First()
            .ToList();

        return new UpNext.Train(
            [.. due.Select(family => family.Family)], due[0].LastTrained, trainedToday, readiness);
    }

    private static DateOnly? LastDay(IEnumerable<PlacedSet> placed) =>
        placed.Select(set => (DateOnly?)set.Day).DefaultIfEmpty(null).Max();
}
