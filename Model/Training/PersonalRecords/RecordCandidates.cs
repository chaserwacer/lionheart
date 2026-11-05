using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;

namespace lionheart.Model.Training.PersonalRecords;

/// <summary>
/// One record a session puts up for comparison: what it is on, and the best the session managed.
/// </summary>
public sealed record RecordCandidate(RecordSubject Subject, Achievement Achievement)
{
    /// <summary>Which record this claims to be, unit included.</summary>
    public RecordKey Key => new(Subject, Achievement.Kind, Achievement.HeldIn);
}

/// <summary>
/// Reading a completed session's sets as the records it might have set.
/// </summary>
/// <remarks>
/// Pure, and deliberately separate from the detector that compares the results against what
/// already stands: what a session achieved is a reading of the session alone, and it is the half
/// worth testing without a database.
/// <para>
/// Candidates are generated per weight unit, not per movement, because the unit is part of a
/// record's identity: the session's best in kilograms and its best in pounds are two records, not
/// two claims on one. A movement logged in both therefore sets and beats a record in each.
/// </para>
/// <para>
/// A set that names no movement at all — <see cref="Guid.Empty"/>, or another id a movement-less
/// subject is stored as — raises no candidate. The session write does not check a set's movement
/// id, so a sentinel can arrive on a lift, and a subject cannot be built from it.
/// </para>
/// <para>
/// A warm-up raises no candidate for any lift record, the session's volume included: it is load
/// the athlete chose to be light, and counting it would let a long warm-up out-rep the work. Drop
/// and failure sets were performed at the weight stated, so they count as working sets do.
/// </para>
/// <para>
/// A set with no load or no reps — and an effort with no distance or no time — raises no
/// candidate. Nothing ranks a zero: it could never beat another zero, so admitting it would plant
/// a record that can only ever be equalled. Logging bodyweight work at zero load therefore sets no
/// records; a reps-at-bodyweight record is a kind this does not yet have.
/// </para>
/// </remarks>
public static class RecordCandidates
{
    public static IReadOnlyList<RecordCandidate> From(CompletedTrainingSession session) =>
        [
            .. FromLifts(session.Sets),
            .. FromMeasuredEfforts(session.Sets),
            .. FromTrailRuns(session.Sets),
            .. FromEndurance(session.Sets),
        ];

    private static IEnumerable<RecordCandidate> FromLifts(IEnumerable<SetEntry> sets) =>
        from set in sets.OfType<LiftSet>()
        where set.IsWorking
        where !RecordSubject.Movement.IsSentinel(set.MovementDataId) && set.Weight > 0 && set.Reps > 0
        group set by (set.MovementDataId, set.Unit) into loaded
        let subject = new RecordSubject.Movement(loaded.Key.MovementDataId)
        let lifts = loaded.Select(set => new LiftPerformance(set.Weight, set.Unit, set.Reps)).ToList()
        from candidate in LiftRecords(subject, loaded.Key.Unit, lifts)
        select candidate;

    private static IEnumerable<RecordCandidate> LiftRecords(
        RecordSubject subject, WeightUnit unit, IReadOnlyList<LiftPerformance> lifts)
    {
        var biggest = lifts.OrderByDescending(lift => lift.Volume).ThenByDescending(lift => lift.Weight).First();
        var total = new SessionVolume(lifts.Sum(lift => lift.Volume), unit);

        return
        [
            new RecordCandidate(subject, new Achievement.HeaviestSet(Heaviest(lifts))),
            new RecordCandidate(subject, new Achievement.BestSetVolume(biggest)),
            new RecordCandidate(subject, new Achievement.BestSessionVolume(total)),
            // Each rep count's best is the heaviest of the sets long enough to hold it, so a
            // heavier set of six takes the five-rep max from a lighter set of five.
            .. from lift in lifts
               from bracket in RecordKind.RepMax.HeldBy(lift.Reps)
               group lift by bracket into held
               select new RecordCandidate(subject, new Achievement.RepMax(held.Key.Reps, Heaviest(held)))
        ];
    }

    /// <summary>
    /// Heavier wins; at equal weight the set that got more reps out of it is the better set.
    /// </summary>
    /// <remarks>
    /// One rule for the heaviest set and every rep max, because the heaviest set is the grid's
    /// one-rep cell: the two must not pick different sets at a tie.
    /// </remarks>
    private static LiftPerformance Heaviest(IEnumerable<LiftPerformance> lifts) =>
        lifts.OrderByDescending(lift => lift.Weight).ThenByDescending(lift => lift.Reps).First();

    private static IEnumerable<RecordCandidate> FromMeasuredEfforts(IEnumerable<SetEntry> sets) =>
        from set in sets.OfType<DistanceTimeSet>()
        where !RecordSubject.Movement.IsSentinel(set.MovementDataId)
        let effort = new DistanceEffort(set.Distance, set.Unit, set.Duration)
        where double.IsFinite(effort.SecondsPerMeter)
        group effort by set.MovementDataId into covered
        from candidate in DistanceRecords(new RecordSubject.Movement(covered.Key), [.. covered])
        select candidate;

    /// <summary>
    /// Trail runs are one subject per athlete rather than one per movement: the set names no
    /// movement, and a trail run's distances are not the distances a measured interval sets
    /// records over.
    /// </summary>
    private static IEnumerable<RecordCandidate> FromTrailRuns(IEnumerable<SetEntry> sets)
    {
        var efforts = (from set in sets.OfType<TrailRunSet>()
                       let effort = new DistanceEffort(set.Distance, set.Unit, set.Duration)
                       where double.IsFinite(effort.SecondsPerMeter)
                       select effort).ToList();

        return efforts.Count == 0 ? [] : DistanceRecords(new RecordSubject.TrailRunning(), efforts);
    }

    /// <summary>
    /// A ride or a hike competes on its own subject, as a trail run does on trail running. An indoor
    /// ride or run has the machine's distance rather than ground covered, an e-bike's is partly the
    /// motor's, a paddle's partly the water's, and a walk is movement rather than an effort to rank,
    /// so none of them raises a candidate.
    /// </summary>
    private static IEnumerable<RecordCandidate> FromEndurance(IEnumerable<SetEntry> sets) =>
        from set in sets.OfType<EnduranceSet>()
        let subject = ContestedSubject(set.Activity)
        where subject is not null
        let effort = new DistanceEffort(set.Distance, set.Unit, set.Duration)
        where double.IsFinite(effort.SecondsPerMeter)
        group effort by subject into covered
        from candidate in DistanceRecords(covered.Key, [.. covered])
        select candidate;

    private static RecordSubject? ContestedSubject(EnduranceActivity activity) => activity switch
    {
        EnduranceActivity.Ride => new RecordSubject.Riding(),
        EnduranceActivity.Hike => new RecordSubject.Hiking(),
        EnduranceActivity.IndoorRide or EnduranceActivity.IndoorRun or EnduranceActivity.Walk
            or EnduranceActivity.EBikeRide or EnduranceActivity.Paddle => null,
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private static IReadOnlyList<RecordCandidate> DistanceRecords(
        RecordSubject subject, IReadOnlyList<DistanceEffort> efforts) =>
    [
        new RecordCandidate(
            subject,
            new Achievement.LongestDistance(efforts.MaxBy(effort => effort.DistanceInMeters)!)),
        new RecordCandidate(
            subject,
            new Achievement.BestPace(efforts.MinBy(effort => effort.SecondsPerMeter)!)),
        .. from effort in efforts
           group effort by effort.BracketMeters into bracket
           select new RecordCandidate(
               subject,
               new Achievement.FastestTime(bracket.MinBy(effort => effort.Duration)!))
    ];
}
