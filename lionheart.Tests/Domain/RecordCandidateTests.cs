using lionheart.Model.Analysis;
using lionheart.Model.Training;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Tests.Support;

namespace lionheart.Tests.Domain;

/// <summary>
/// What a completed session claims to have achieved, read off its sets alone.
/// </summary>
public class RecordCandidateTests
{
    private static readonly Guid Bench = Guid.NewGuid();
    private static readonly Guid Treadmill = Guid.NewGuid();

    [Fact]
    public void A_movements_lift_sets_raise_its_heaviest_set_its_best_set_volume_and_its_session_total()
    {
        var candidates = From(
            Lift(Bench, weight: 225, reps: 5),
            Lift(Bench, weight: 245, reps: 2),
            Lift(Bench, weight: 185, reps: 10));

        Assert.Equal(
            new LiftPerformance(245, WeightUnit.Pounds, 2),
            Single<Achievement.HeaviestSet>(candidates).Lift);
        Assert.Equal(
            new LiftPerformance(185, WeightUnit.Pounds, 10),
            Single<Achievement.BestSetVolume>(candidates).Lift);
        Assert.Equal(
            new SessionVolume(225 * 5 + 245 * 2 + 185 * 10, WeightUnit.Pounds),
            Single<Achievement.BestSessionVolume>(candidates).Total);
    }

    [Fact]
    public void At_equal_weight_the_set_that_got_more_reps_is_the_heavier_set()
    {
        var candidates = From(Lift(Bench, 225, reps: 3), Lift(Bench, 225, reps: 6));

        Assert.Equal(6, Single<Achievement.HeaviestSet>(candidates).Lift.Reps);
    }

    [Fact]
    public void Each_rep_count_in_the_session_raises_its_own_rep_max()
    {
        var candidates = From(
            Lift(Bench, 245, reps: 3),
            Lift(Bench, 225, reps: 5),
            Lift(Bench, 215, reps: 5));

        var grid = All<Achievement.RepMax>(candidates);

        Assert.Equal(2, grid.Count);
        Assert.Equal(245, grid.Single(max => max.Lift.Reps == 3).Lift.Weight);
        // Two sets at five reps: only the heavier one is the claim.
        Assert.Equal(225, grid.Single(max => max.Lift.Reps == 5).Lift.Weight);
    }

    [Fact]
    public void A_set_above_the_graded_rep_ceiling_sets_no_rep_max_and_still_counts_everywhere_else()
    {
        // A 23-rep set is a fact about work capacity, which the volume records already answer.
        var candidates = From(Lift(Bench, 95, reps: 23));

        Assert.Empty(All<Achievement.RepMax>(candidates));
        Assert.Equal(95, Single<Achievement.HeaviestSet>(candidates).Lift.Weight);
        Assert.Equal(95 * 23, Single<Achievement.BestSetVolume>(candidates).Lift.Volume);
        Assert.Equal(95 * 23, Single<Achievement.BestSessionVolume>(candidates).Total.Volume);
    }

    [Fact]
    public void The_rep_max_grid_runs_to_twelve_inclusive()
    {
        var candidates = From(Lift(Bench, 135, reps: 12), Lift(Bench, 115, reps: 13));

        Assert.Equal(12, Assert.Single(All<Achievement.RepMax>(candidates)).Lift.Reps);
    }

    [Fact]
    public void A_set_with_no_load_or_no_reps_claims_nothing()
    {
        // Nothing ranks a zero: it could never beat another zero, so admitting it would plant a
        // record that can only ever be equalled. Bodyweight work logged at zero load therefore
        // sets no records.
        Assert.Empty(From(Lift(Bench, weight: 0, reps: 10)));
        Assert.Empty(From(Lift(Bench, weight: 225, reps: 0)));
    }

    [Fact]
    public void Two_units_on_one_movement_raise_a_record_each_rather_than_two_claims_on_one()
    {
        // The unit is part of a record's identity, because weights never convert: the session's
        // best in kilograms and its best in pounds are two records, not two bids for one.
        var candidates = From(
            Lift(Bench, 100, reps: 5, unit: WeightUnit.Kilograms),
            Lift(Bench, 225, reps: 5, unit: WeightUnit.Pounds));

        var heaviest = All<Achievement.HeaviestSet>(candidates);

        Assert.Equal(2, heaviest.Count);
        Assert.Equal(
            [WeightUnit.Kilograms, WeightUnit.Pounds],
            heaviest.Select(set => set.Lift.Unit).OrderBy(unit => unit));
        Assert.Equal(
            2,
            candidates
                .Where(candidate => candidate.Achievement is Achievement.HeaviestSet)
                .Select(candidate => candidate.Key)
                .Distinct()
                .Count());
    }

    [Fact]
    public void A_set_that_names_no_movement_claims_nothing()
    {
        // Guid.Empty is the storage sentinel for trail running, and the session write does not
        // check a set's movement id — so it can arrive on a lift, where no subject can be built
        // from it. Raising nothing is what keeps the session loggable.
        Assert.Empty(From(Lift(Guid.Empty, 225, reps: 5)));
        Assert.Empty(From(Measured(Guid.Empty, 5, DistanceUnit.Kilometers, TimeSpan.FromMinutes(25))));
    }

    [Fact]
    public void A_sentinel_set_beside_a_real_one_leaves_the_real_one_claiming()
    {
        var candidates = From(Lift(Guid.Empty, 405, reps: 1), Lift(Bench, 225, reps: 5));

        Assert.Equal(225, Single<Achievement.HeaviestSet>(candidates).Lift.Weight);
    }

    [Fact]
    public void Two_movements_are_two_subjects()
    {
        var squat = Guid.NewGuid();

        var subjects = From(Lift(Bench, 225, 5), Lift(squat, 315, 5))
            .Select(candidate => candidate.Subject)
            .Distinct()
            .ToList();

        Assert.Equal(2, subjects.Count);
    }

    [Fact]
    public void A_measured_effort_raises_its_distance_its_pace_and_its_time_at_that_distance()
    {
        var candidates = From(
            Measured(Treadmill, 5, DistanceUnit.Kilometers, TimeSpan.FromMinutes(25)),
            Measured(Treadmill, 3, DistanceUnit.Kilometers, TimeSpan.FromMinutes(13)));

        Assert.Equal(5, Single<Achievement.LongestDistance>(candidates).Effort.Distance);
        // 13 minutes for three kilometres is the quicker of the two per kilometre.
        Assert.Equal(3, Single<Achievement.BestPace>(candidates).Effort.Distance);
        Assert.Equal(
            [3000, 5000],
            All<Achievement.FastestTime>(candidates)
                .Select(fastest => fastest.Effort.BracketMeters)
                .OrderBy(bracket => bracket));
    }

    [Fact]
    public void Repeating_a_distance_keeps_only_the_quicker_time()
    {
        var candidates = From(
            Measured(Treadmill, 5, DistanceUnit.Kilometers, TimeSpan.FromMinutes(25)),
            Measured(Treadmill, 5, DistanceUnit.Kilometers, TimeSpan.FromMinutes(23)));

        Assert.Equal(
            TimeSpan.FromMinutes(23),
            Assert.Single(All<Achievement.FastestTime>(candidates)).Effort.Duration);
    }

    [Fact]
    public void A_trail_run_claims_against_trail_running_rather_than_against_a_movement()
    {
        // A trail set names no movement to hang a record off, so trail running is a subject of its
        // own and a trail run is never ranked against a measured treadmill effort.
        var candidates = From(
            TrailRun(8, DistanceUnit.Kilometers, TimeSpan.FromMinutes(70)),
            Measured(Treadmill, 10, DistanceUnit.Kilometers, TimeSpan.FromMinutes(50)));

        var bySubject = candidates.GroupBy(candidate => candidate.Subject).ToList();

        Assert.Equal(2, bySubject.Count);
        Assert.Contains(bySubject, group => group.Key is RecordSubject.TrailRunning);
        Assert.Equal(
            8,
            candidates
                .Where(candidate => candidate.Subject is RecordSubject.TrailRunning)
                .Select(candidate => candidate.Achievement)
                .OfType<Achievement.LongestDistance>()
                .Single().Effort.Distance);
    }

    [Fact]
    public void Every_trail_run_in_a_session_competes_as_one_subject()
    {
        var candidates = From(
            TrailRun(8, DistanceUnit.Kilometers, TimeSpan.FromMinutes(70)),
            TrailRun(12, DistanceUnit.Kilometers, TimeSpan.FromMinutes(100)));

        Assert.Equal(12, Single<Achievement.LongestDistance>(candidates).Effort.Distance);
    }

    [Fact]
    public void A_freeform_set_claims_nothing() =>
        Assert.Empty(RecordCandidates.From(SessionOf(Some.FreeformSet)));

    [Fact]
    public void A_session_of_nothing_measurable_claims_nothing() =>
        Assert.Empty(RecordCandidates.From(SessionOf()));

    private static IReadOnlyList<RecordCandidate> From(params SetEntry[] sets) =>
        RecordCandidates.From(SessionOf(sets));

    private static CompletedTrainingSession SessionOf(params SetEntry[] sets) =>
        new(sets, new Instant(Some.Clock), new EffortNotReported(), new NotDecorated())
        {
            TrainingSessionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Notes = string.Empty,
            Provenance = Provenance.User,
            Membership = new Standalone(),
            CreatedAt = Some.Clock,
        };

    private static LiftSet Lift(Guid movementDataId, double weight, int reps, WeightUnit unit = WeightUnit.Pounds) =>
        new(movementDataId, reps, weight, unit, Rpe.Unrated) { Source = new Formed(), Facets = [] };

    private static DistanceTimeSet Measured(
        Guid movementDataId, double distance, DistanceUnit unit, TimeSpan duration) =>
        new(movementDataId, distance, unit, duration, Rpe.Unrated) { Source = new Formed(), Facets = [] };

    private static TrailRunSet TrailRun(double distance, DistanceUnit unit, TimeSpan duration) =>
        new(distance, unit, duration, ElevationGainMeters: 400, Rpe.Unrated)
        {
            Source = new Formed(),
            Facets = [],
        };

    private static T Single<T>(IReadOnlyList<RecordCandidate> candidates) where T : Achievement =>
        Assert.Single(All<T>(candidates));

    private static IReadOnlyList<T> All<T>(IReadOnlyList<RecordCandidate> candidates) where T : Achievement =>
        [.. candidates.Select(candidate => candidate.Achievement).OfType<T>()];
}
