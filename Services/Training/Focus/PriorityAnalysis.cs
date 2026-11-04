using lionheart.Model.Training.Balance;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Sets;
using lionheart.Services.Training.Balance;

namespace lionheart.Services.Training.Focus;

/// <summary>
/// Each family's priority lift over a window, read from the pins and the log: which lift, how its
/// best compares with the twelve weeks before, and how often it was trained.
/// </summary>
/// <remarks>Pure, so the score and the priorities screen read one answer and it is tested without a database.</remarks>
public static class PriorityAnalysis
{
    /// <summary>How far back the window's best is compared: twelve weeks, about one training block.</summary>
    public const int ComparisonDays = 84;

    /// <summary>How far back the most-trained lift is looked for when no priority is pinned.</summary>
    public const int ImpliedLookBackDays = 56;

    public const int FrequencyDays = 28;

    /// <summary>Sets of more reps than this estimate nothing, the cap the athlete's own analysis uses.</summary>
    public const int MostRepsEstimated = 12;

    public static IReadOnlyList<FamilyPriority> Read(TrainingLog log, IReadOnlyList<FocusPin> pins, DateOnly endingOn)
    {
        var window = VolumeTargets.WindowEndingOn(endingOn);
        return
        [
            .. MovementPatterns.Judged.Select(family => new FamilyPriority(
                family,
                Choose(log, pins, family, endingOn) is var (movementDataId, choice)
                    ? Lift(log, movementDataId, choice, window.From, endingOn)
                    : new PriorityLift.NoLift()))
        ];
    }

    /// <summary>The pin in force on the day, else the family's most-trained compound lift, else nothing.</summary>
    /// <remarks>
    /// The newest pin wins where two stand on one day: a priority that superseded another unpins it
    /// on that one's own first day when it starts earlier, and on that day both are in force.
    /// </remarks>
    private static (Guid MovementDataId, PriorityChoice Choice)? Choose(
        TrainingLog log, IReadOnlyList<FocusPin> pins, PatternFamily family, DateOnly day)
    {
        var pinned = pins
            .Where(pin => pin.Role.IsPriorityFor(family) && InForce(pin, day))
            .MaxBy(pin => (pin.PinnedAt, pin.Terms.Horizon.Starts));
        if (pinned is not null) return (pinned.MovementDataId, new PriorityChoice.ByPin(FocusPinView.Of(pin: pinned)));

        var implied = log.Between(day.AddDays(1 - ImpliedLookBackDays), day)
            .SelectMany(session => session.HardSets
                .Where(set => log.ClassificationOf(set.MovementDataId) is ClassificationStanding.Classified
                {
                    Classification: { } trains,
                } && trains.Family == family && MovementPatterns.Compound.Contains(trains.Pattern))
                .Select(set => (session.Day, session.TrainingSessionId, set.MovementDataId)))
            .GroupBy(set => set.MovementDataId)
            .Select(group => (
                MovementDataId: group.Key,
                Sessions: group.Select(set => set.TrainingSessionId).Distinct().Count(),
                Sets: group.Count(),
                Last: group.Max(set => set.Day),
                Name: log.NameOf(group.Key)))
            .OrderByDescending(lift => lift.Sessions)
            .ThenByDescending(lift => lift.Sets)
            .ThenByDescending(lift => lift.Last)
            .ThenBy(lift => lift.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return implied is [var most, ..]
            ? (most.MovementDataId, new PriorityChoice.MostTrained(most.Sessions))
            : null;
    }

    /// <summary>Whether a pin stood on the day: inside its horizon, and not yet let go.</summary>
    public static bool InForce(FocusPin pin, DateOnly day) =>
        day >= pin.Terms.Horizon.Starts && day <= pin.Terms.Horizon.Ends && pin.Status switch
        {
            FocusStatus.Pinned => true,
            FocusStatus.Unpinned unpinned => day <= unpinned.On,
            _ => throw new System.Diagnostics.UnreachableException(),
        };

    private static PriorityLift.Chosen Lift(
        TrainingLog log, Guid movementDataId, PriorityChoice choice, DateOnly windowFrom, DateOnly endingOn)
    {
        var sessions = log.Between(DateOnly.MinValue, endingOn)
            .Select(session => (session.Day, Sets: session.HardSets.Where(set => set.MovementDataId == movementDataId).ToList()))
            .Where(session => session.Sets.Count > 0)
            .ToList();

        var unit = UnitOf(log, movementDataId, endingOn);
        var bests = sessions
            .Select(session => Best(log, session.Day, session.Sets, unit))
            .OfType<SessionBest>()
            .ToList();

        var compareFrom = windowFrom.AddDays(-ComparisonDays);
        return new PriorityLift.Chosen(
            movementDataId,
            log.NameOf(movementDataId),
            choice,
            Progress(
                trained: sessions.Any(session => session.Day >= windowFrom),
                inWindow: Max(bests.Where(best => best.Day >= windowFrom)),
                prior: Max(bests.Where(best => best.Day >= compareFrom && best.Day < windowFrom)),
                allTime: Max(bests.Where(best => best.Day < windowFrom)),
                unit,
                OnSystemLoad(log, movementDataId, unit)),
            sessions.Count(session => session.Day > endingOn.AddDays(-FrequencyDays)),
            [.. bests.Where(best => best.Day >= compareFrom)]);
    }

    private static LiftProgress Progress(
        bool trained, double? inWindow, double? prior, double? allTime, WeightUnit unit, bool onSystemLoad) =>
        (trained, inWindow, prior) switch
        {
            (false, _, _) => new LiftProgress.NotTrained(),
            (true, null, _) => new LiftProgress.NoEstimate(),
            (true, { } best, null) => new LiftProgress.FirstBack(best, unit, onSystemLoad),
            (true, { } best, { } before) => new LiftProgress.Compared(best, before, allTime ?? before, unit, onSystemLoad),
        };

    /// <summary>The session's best single by Epley, on the load each set moved, in the lift's unit.</summary>
    private static SessionBest? Best(TrainingLog log, DateOnly day, IReadOnlyList<LiftSet> sets, WeightUnit unit) =>
        sets
            .Where(set => set.Unit == unit && set.Reps is >= 1 and <= MostRepsEstimated)
            .Select(set => (Load: log.LoadOf(set, day), set.Reps))
            .Where(set => set.Load > 0)
            .Select(set => new SessionBest(
                day, new LiftPerformance(set.Load, unit, set.Reps).EstimatedOneRepMax, set.Load, set.Reps))
            .MaxBy(best => best.EstimatedOneRepMax);

    /// <summary>The unit a lift was last logged in on or before the day; pounds for one never logged.</summary>
    public static WeightUnit UnitOf(TrainingLog log, Guid movementDataId, DateOnly endingOn) =>
        log.Between(DateOnly.MinValue, endingOn)
            .SelectMany(session => session.HardSets)
            .LastOrDefault(set => set.MovementDataId == movementDataId)?.Unit ?? WeightUnit.Pounds;

    /// <summary>Whether a lift's estimates are on system load: its weight adds to bodyweight, and bodyweight is logged in its unit.</summary>
    public static bool OnSystemLoad(TrainingLog log, Guid movementDataId, WeightUnit unit) =>
        log.Movements.TryGetValue(movementDataId, out var movement)
        && movement.AddsToBodyweight
        && log.Bodyweights.Any(measured => measured.Bodyweight.Unit == unit);

    private static double? Max(IEnumerable<SessionBest> bests) =>
        bests.Select(best => (double?)best.EstimatedOneRepMax).DefaultIfEmpty(null).Max();
}
