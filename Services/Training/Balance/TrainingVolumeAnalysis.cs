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
        log.Between(from, to).SelectMany(session => session.HardSets
            .Select(set => (Set: set, Standing: log.ClassificationOf(set.MovementDataId)))
            .Where(placed => placed.Standing is ClassificationStanding.Classified)
            .Select(placed => new PlacedSet(
                session.Day, placed.Set, ((ClassificationStanding.Classified)placed.Standing).Classification)));

    /// <summary>Push, pull and legs, each set counted once toward its movement's family.</summary>
    public static IReadOnlyList<FamilyVolume> Families(IReadOnlyList<PlacedSet> inWindow, IReadOnlyList<PlacedSet> history) =>
    [
        .. MovementPatterns.Judged.Select(family => new FamilyVolume(
            family,
            inWindow.Count(placed => placed.Trains.Family == family),
            VolumeTargets.Family,
            LastDay(history.Where(placed => placed.Trains.Family == family))))
    ];

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

/// <summary>Reads a day's readiness against the athlete's own usual range.</summary>
public static class ReadinessNotes
{
    /// <summary>The days of history the usual range is drawn from.</summary>
    public const int UsualRangeDays = 90;

    /// <summary>The fewest scored days a usual range is read from; fewer and its tails are noise.</summary>
    public const int FewestDays = 30;

    /// <param name="scores">Oura's readiness scores by day, any order; days with none are left out.</param>
    public static ReadinessNote Of(IReadOnlyList<(DateOnly Day, int Score)> scores, DateOnly day)
    {
        var today = scores.Where(score => score.Day == day).Select(score => (int?)score.Score).FirstOrDefault();
        var before = scores
            .Where(score => score.Day < day && score.Day >= day.AddDays(-UsualRangeDays))
            .Select(score => score.Score)
            .Order()
            .ToList();

        if (today is not { } score || before.Count < FewestDays) return new ReadinessNote.NotRead();

        var low = Percentile(before, 0.1);
        var high = Percentile(before, 0.9);
        return score < low ? new ReadinessNote.Low(score, low, high) : new ReadinessNote.Usual(score);
    }

    /// <summary>Nearest-rank percentile of a sorted list.</summary>
    private static int Percentile(IReadOnlyList<int> sorted, double fraction) =>
        sorted[(int)Math.Floor(fraction * (sorted.Count - 1))];
}
