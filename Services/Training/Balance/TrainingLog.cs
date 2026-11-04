using lionheart.Model.BodyMeasurements;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Sets;

namespace lionheart.Services.Training.Balance;

/// <summary>
/// The athlete's completed training as volume, priorities and the score read it: every session's
/// day and hard sets, what each movement trains, and his bodyweight over time.
/// </summary>
/// <remarks>
/// Read once and handed to pure functions, so a year of weekly scores is computed from one read
/// rather than one per week. Sessions are oldest first.
/// </remarks>
public sealed record TrainingLog(
    IReadOnlyList<SessionInLog> Sessions,
    IReadOnlyDictionary<Guid, LoggedMovement> Movements,
    IReadOnlyList<BodyweightOnDay> Bodyweights)
{
    public static readonly TrainingLog Empty = new([], new Dictionary<Guid, LoggedMovement>(), []);

    public IEnumerable<SessionInLog> Between(DateOnly from, DateOnly to) =>
        Sessions.Where(session => session.Day >= from && session.Day <= to);

    /// <summary>What a movement trains, or unclassified for one the log has no record of.</summary>
    public ClassificationStanding ClassificationOf(Guid movementDataId) =>
        Movements.TryGetValue(movementDataId, out var movement)
            ? movement.Classification
            : new ClassificationStanding.Unclassified();

    public string NameOf(Guid movementDataId) =>
        Movements.TryGetValue(movementDataId, out var movement) ? movement.Name : "Unknown movement";

    /// <summary>
    /// The load a set moved: bodyweight plus the plate for a movement whose weight adds to it,
    /// where any bodyweight in the set's unit is logged; else the logged weight.
    /// </summary>
    /// <remarks>
    /// Bodyweight on a day is the newest measurement on or before it, the decision log's rule.
    /// Before the first measurement the first one stands in: otherwise a lift's history would jump
    /// by a whole bodyweight on the day weighing began, and every set after it would read as a
    /// lifetime best. Weights never convert, so a bodyweight in the other unit is no bodyweight for
    /// this set.
    /// </remarks>
    public double LoadOf(LiftSet set, DateOnly day)
    {
        if (!Movements.TryGetValue(set.MovementDataId, out var movement) || !movement.AddsToBodyweight) return set.Weight;

        return BodyweightOn(Bodyweights, day, set.Unit) is { } bodyweight ? bodyweight.Value + set.Weight : set.Weight;
    }

    /// <summary>
    /// The bodyweight a set on this day adds its plate to: the newest measurement in the unit on or
    /// before the day, else the first one in the unit; none where nothing is measured in it.
    /// </summary>
    /// <remarks>Shared with the Health import, which counts the sets a backfill of weigh-ins re-reads.</remarks>
    public static Bodyweight? BodyweightOn(IReadOnlyList<BodyweightOnDay> bodyweights, DateOnly day, WeightUnit unit)
    {
        var inUnit = bodyweights.Where(measured => measured.Bodyweight.Unit == unit).ToList();
        return (inUnit.Where(measured => measured.Day <= day).MaxBy(measured => measured.Day)
                ?? inUnit.MinBy(measured => measured.Day))?.Bodyweight;
    }
}

/// <summary>One completed session: its day on the athlete's own clock, and its hard sets.</summary>
/// <remarks>A session of runs or freeform sets has no hard sets and still counts as a session.</remarks>
public sealed record SessionInLog(Guid TrainingSessionId, DateOnly Day, IReadOnlyList<LiftSet> HardSets)
{
    /// <summary>
    /// Nothing but walking: a watch's record of getting somewhere, which is in the log but is not a
    /// training session.
    /// </summary>
    public bool OnlyWalking { get; init; }

    /// <summary>Whether the session counts as one trained: anything but a session of only walking.</summary>
    public bool Trained => !OnlyWalking;
}

/// <summary>A movement as the log reads it: a display name, what it trains, and how its weight is read.</summary>
public sealed record LoggedMovement(Guid MovementDataId, string Name, ClassificationStanding Classification, bool AddsToBodyweight);

public sealed record BodyweightOnDay(DateOnly Day, Bodyweight Bodyweight);

/// <summary>Which lifts are hard sets.</summary>
public static class HardSets
{
    /// <summary>A working, drop or failure set of a lift. A warm-up is preparation, not training stimulus.</summary>
    public static bool IsHard(LiftSet set) => set.Kind is not SetKind.WarmUp;
}
