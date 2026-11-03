using lionheart.Model.Analysis;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Tests.Api;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Services;

/// <summary>
/// Completing a session establishes the records its sets set, against a real database and through
/// the route an athlete actually uses.
/// </summary>
public class PersonalRecordDetectionTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Performed = new(2026, 9, 10, 17, 0, 0, TimeSpan.FromHours(-4));

    private readonly TwoAthletes _athletes = new();
    private Guid _bench;

    public async Task InitializeAsync() =>
        _bench = await _athletes.PlantMovementAsync(_athletes.OwnerId, "Bench Press");

    public Task DisposeAsync()
    {
        _athletes.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task A_first_session_establishes_the_records_its_sets_set()
    {
        await LogAsync(Lift(weight: 225, reps: 5));

        var records = await StandingAsync();

        // Heaviest set, best set volume, the session total, and the rep maxes at two through five,
        // every one of them held by the one set of five.
        Assert.Equal(7, records.Count);
        Assert.Equal(
            new LiftPerformance(225, WeightUnit.Pounds, 5),
            Standing<Achievement.HeaviestSet>(records).Lift);
        Assert.Equal(
            [2, 3, 4, 5],
            records.Select(record => record.Achievement).OfType<Achievement.RepMax>()
                .Select(max => max.Reps).Order());
        Assert.All(records, record => Assert.IsType<RecordLineage.FirstOfItsKind>(record.Lineage));
    }

    [Fact]
    public async Task A_record_is_dated_the_day_it_was_performed_rather_than_the_day_it_was_logged()
    {
        // A session logged a fortnight late set its records on the day it happened; stamping the
        // write instant would put the whole progression in the wrong week.
        await LogAsync(Lift(225, 5));

        var heaviest = StandingRecord<Achievement.HeaviestSet>(await StandingAsync());

        Assert.Equal(Performed.UtcDateTime, heaviest.AchievedAt);
    }

    [Fact]
    public async Task Beating_a_record_leaves_the_old_one_beaten_and_pointed_at()
    {
        await LogAsync(Lift(225, 5));
        await LogAsync(Lift(245, 5));

        var heaviest = StandingRecord<Achievement.HeaviestSet>(await StandingAsync());

        Assert.Equal(245, Assert.IsType<Achievement.HeaviestSet>(heaviest.Achievement).Lift.Weight);
        var replaced = Assert.IsType<RecordLineage.Replaced>(heaviest.Lineage);
        Assert.Equal(Performed.UtcDateTime, replaced.AchievedAt);

        var beaten = await ChainAsync(new RecordKind.HeaviestSet());
        Assert.Equal(2, beaten.Count);
        Assert.IsType<RecordStanding.Beaten>(beaten.Single(r => !r.IsStanding).Standing);
    }

    [Fact]
    public async Task A_session_whose_sets_each_beat_the_record_adds_one_link_rather_than_one_per_set()
    {
        // The sets in between never stood as records, so a row each would read as four records set
        // in the same instant with no way to tell which actually held.
        await LogAsync(Lift(225, 5));
        await LogAsync(Lift(230, 5), Lift(235, 5), Lift(240, 5));

        var chain = await ChainAsync(new RecordKind.HeaviestSet());

        Assert.Equal(2, chain.Count);
        Assert.Equal(
            240,
            Assert.IsType<Achievement.HeaviestSet>(
                chain.Single(record => record.IsStanding).Achievement).Lift.Weight);
        Assert.Equal(
            225,
            Assert.IsType<Achievement.HeaviestSet>(
                chain.Single(record => !record.IsStanding).Achievement).Lift.Weight);
    }

    [Fact]
    public async Task A_session_that_beats_nothing_writes_nothing()
    {
        await LogAsync(Lift(225, 5));
        var before = (await StandingAsync()).Select(record => record.PersonalRecordId).ToList();

        await LogAsync(Lift(205, 5));

        // Same movement, same unit, same rep count, so every identity the lighter session claims
        // already stands and none of its claims beats what holds it. No row at all is written.
        using var context = _athletes.CreateContext();
        Assert.Equal(before.Count, await context.PersonalRecords.CountAsync());
    }

    [Fact]
    public async Task A_rep_count_never_trained_before_opens_its_own_grid_row()
    {
        await LogAsync(Lift(225, 5));
        await LogAsync(Lift(185, 8));

        var grid = await GridAsync();

        Assert.Equal(225, grid[5].Weight);
        Assert.Equal(new LiftPerformance(185, WeightUnit.Pounds, 8), grid[8]);
        Assert.Single(await ChainAsync(new RecordKind.RepMax(8)));
    }

    [Fact]
    public async Task A_heavier_shorter_set_takes_the_rep_maxes_it_contains_and_leaves_the_longer_ones()
    {
        await LogAsync(Lift(225, 5));
        await LogAsync(Lift(255, 3));

        var grid = await GridAsync();

        Assert.Equal(new LiftPerformance(255, WeightUnit.Pounds, 3), grid[2]);
        Assert.Equal(new LiftPerformance(255, WeightUnit.Pounds, 3), grid[3]);
        Assert.Equal(new LiftPerformance(225, WeightUnit.Pounds, 5), grid[4]);
        Assert.Equal(new LiftPerformance(225, WeightUnit.Pounds, 5), grid[5]);
        // The three the set of five held is a link the set of three beat, not a separate record.
        Assert.IsType<RecordLineage.Replaced>(
            Assert.Single(await ChainAsync(new RecordKind.RepMax(3)), record => record.IsStanding).Lineage);
    }

    [Fact]
    public async Task A_longer_set_beats_a_rep_max_held_by_a_shorter_one()
    {
        // 185 for eight is a heavier five than 180 for five, and the record says it came from the
        // eight.
        await LogAsync(Lift(180, 5));
        await LogAsync(Lift(185, 8));

        var fives = await ChainAsync(new RecordKind.RepMax(5));

        Assert.Equal(2, fives.Count);
        var standing = Assert.IsType<Achievement.RepMax>(Assert.Single(fives, record => record.IsStanding).Achievement);
        Assert.Equal(new Achievement.RepMax(5, new LiftPerformance(185, WeightUnit.Pounds, 8)), standing);
    }

    [Fact]
    public async Task A_single_sets_the_heaviest_set_and_no_rep_max()
    {
        // The one-rep max is the heaviest set: every set contains a single, so a rep max at one
        // would be the same fact stored twice.
        await LogAsync(Lift(275, 1));

        var records = await StandingAsync();

        Assert.Empty(records.Select(record => record.Achievement).OfType<Achievement.RepMax>());
        Assert.Equal(275, Standing<Achievement.HeaviestSet>(records).Lift.Weight);
    }

    [Fact]
    public async Task A_lift_in_the_other_unit_leaves_the_record_alone_and_opens_its_own()
    {
        // 100 kg is a heavier lift and a smaller number, and nothing converts — so it is not a
        // better pounds record. It is a different record, and the unit being part of the identity
        // is what lets it become one rather than vanishing.
        await LogAsync(Lift(225, 5));
        await LogAsync(Lift(100, 5, WeightUnit.Kilograms));

        var heaviest = (await StandingAsync())
            .Select(record => record.Achievement)
            .OfType<Achievement.HeaviestSet>()
            .ToList();

        Assert.Equal(2, heaviest.Count);
        Assert.Equal(225, heaviest.Single(set => set.Lift.Unit == WeightUnit.Pounds).Lift.Weight);
        Assert.Equal(100, heaviest.Single(set => set.Lift.Unit == WeightUnit.Kilograms).Lift.Weight);
    }

    /// <summary>
    /// Switching units does not end a movement's progression.
    /// </summary>
    /// <remarks>
    /// With the unit only a comparability test, the kilograms set could neither beat the standing
    /// pounds record nor become one, so the athlete could never set a record on that movement
    /// again. The unit belongs in the identity, which is also how the iOS trends chart keys its
    /// strength series so a unit switch starts a new line rather than putting a cliff in one.
    /// </remarks>
    [Fact]
    public async Task After_switching_units_the_athlete_can_still_set_and_beat_records()
    {
        await LogAsync(Lift(225, 5));
        await LogAsync(Lift(100, 5, WeightUnit.Kilograms));
        await LogAsync(Lift(105, 5, WeightUnit.Kilograms));

        var inKilograms = await ChainAsync(
            new RecordKind.HeaviestSet(), WeightUnit.Kilograms);

        Assert.Equal(2, inKilograms.Count);
        Assert.Equal(
            105,
            Assert.IsType<Achievement.HeaviestSet>(
                Assert.Single(inKilograms, record => record.IsStanding).Achievement).Lift.Weight);
        // And the pounds record is untouched by any of it.
        Assert.Single(await ChainAsync(new RecordKind.HeaviestSet(), WeightUnit.Pounds));
    }

    [Fact]
    public async Task A_set_on_a_movement_that_is_not_the_athletes_sets_no_record()
    {
        // The session write never checks a set's movement id, so without this a mistyped one would
        // open a record on a movement that does not exist.
        var strangers = await _athletes.PlantMovementAsync(_athletes.StrangerId, "Someone else's bench");

        await LogAsync(Lift(225, 5, movementDataId: strangers));

        Assert.Empty(await StandingAsync());
    }

    [Fact]
    public async Task Completing_an_active_session_establishes_its_records_too()
    {
        var started = ResultAssert.Succeeded(await _athletes.SessionsAsync(service =>
            service.StartUnplannedAsync(
                _athletes.Owner, new StartUnplannedSessionRequest(Performed, "bench day", null), default)));

        ResultAssert.Succeeded(await _athletes.SessionsAsync(service =>
            service.ReplaceSetsAsync(
                _athletes.Owner,
                started.TrainingSessionId,
                new ReplaceSetsRequest([Command(225, 5)]),
                default)));

        // Nothing is a record until the sets are final: an active session's sets are replaced
        // whole on every write, so a record set from one would have to be taken away again.
        Assert.Empty(await StandingAsync());

        ResultAssert.Succeeded(await _athletes.SessionsAsync(service =>
            service.CompleteAsync(
                _athletes.Owner,
                started.TrainingSessionId,
                new CompleteSessionRequest(new PeriodCommand(Performed, null), null),
                default)));

        // Heaviest set, best set volume, session total, and the rep maxes at two through five.
        Assert.Equal(7, (await StandingAsync()).Count);
    }

    [Fact]
    public async Task A_refused_completion_establishes_nothing()
    {
        // The staged-failure guarantee: the transition is refused before anything is staged, so no
        // record exists for a session that never completed.
        var planned = ResultAssert.Succeeded(await _athletes.SessionsAsync(service =>
            service.PlanAsync(
                _athletes.Owner,
                new PlanSessionRequest(
                    DateOnly.FromDateTime(Performed.DateTime), [], "tomorrow", null),
                default)));

        ResultAssert.Failed(await _athletes.SessionsAsync(service =>
            service.CompleteAsync(
                _athletes.Owner,
                planned.TrainingSessionId,
                new CompleteSessionRequest(new PeriodCommand(Performed, null), null),
                default)));

        Assert.Empty(await StandingAsync());
    }

    [Fact]
    public async Task Trail_running_holds_its_records_apart_from_a_measured_treadmill_effort()
    {
        var treadmill = await _athletes.PlantMovementAsync(_athletes.OwnerId, "Treadmill");

        await LogAsync(
            new TrailRunSetCommand(8, DistanceUnit.Kilometers, TimeSpan.FromMinutes(70), 600, null),
            new DistanceTimeSetCommand(treadmill, 10, DistanceUnit.Kilometers, TimeSpan.FromMinutes(50), null));

        var bySubject = (await StandingAsync()).GroupBy(record => record.Subject).ToList();

        Assert.Equal(2, bySubject.Count);
        var trail = bySubject.Single(group => group.Key is RecordSubject.TrailRunning);
        Assert.Equal(
            8,
            trail.Select(record => record.Achievement)
                .OfType<Achievement.LongestDistance>()
                .Single().Effort.Distance);
    }

    [Fact]
    public async Task One_athletes_records_are_invisible_to_another()
    {
        await LogAsync(Lift(225, 5));

        using var context = _athletes.CreateContext();
        Assert.Empty(await context.PersonalRecords
            .Where(record => record.UserId == _athletes.StrangerId)
            .ToListAsync());
    }

    // ---- Logging a session the way the route does ----

    private Task LogAsync(params SetCommand[] sets) =>
        _athletes.SessionsAsync(service => service.LogCompletedAsync(
            _athletes.Owner,
            new LogCompletedSessionRequest(
                [.. sets], new PeriodCommand(Performed, null), null, "logged", null),
            default));

    private SetCommand Lift(
        double weight, int reps, WeightUnit unit = WeightUnit.Pounds, Guid? movementDataId = null) =>
        new LiftSetCommand(movementDataId ?? _bench, reps, weight, unit, null);

    private SetCommand Command(double weight, int reps) => Lift(weight, reps);

    private async Task<IReadOnlyList<PersonalRecord>> StandingAsync()
    {
        using var context = _athletes.CreateContext();
        var mine = await context.PersonalRecords
            .AsNoTracking()
            .Where(record => record.UserId == _athletes.OwnerId)
            .ToListAsync();

        return [.. mine.Where(record => record.IsStanding)];
    }

    /// <summary>Every record ever set of one kind on the bench, newest first.</summary>
    private async Task<IReadOnlyList<PersonalRecord>> ChainAsync(
        RecordKind kind, WeightUnit? heldIn = WeightUnit.Pounds)
    {
        using var context = _athletes.CreateContext();
        var all = await context.PersonalRecords.AsNoTracking()
            .Where(record => record.UserId == _athletes.OwnerId)
            .ToListAsync();

        return
        [
            .. all
                .Where(record => record.Kind == kind && record.Achievement.HeldIn == heldIn)
                .OrderByDescending(record => record.AchievedAt)
                .ThenByDescending(record => record.PersonalRecordId)
        ];
    }

    /// <summary>The bench's standing rep maxes in pounds, by rep count, each with the set holding it.</summary>
    private async Task<IReadOnlyDictionary<int, LiftPerformance>> GridAsync() =>
        (await StandingAsync())
            .Select(record => record.Achievement)
            .OfType<Achievement.RepMax>()
            .Where(max => max.Lift.Unit == WeightUnit.Pounds)
            .ToDictionary(max => max.Reps, max => max.Lift);

    private static T Standing<T>(IReadOnlyList<PersonalRecord> records) where T : Achievement =>
        Assert.Single(records.Select(record => record.Achievement).OfType<T>());

    private static PersonalRecord StandingRecord<T>(IReadOnlyList<PersonalRecord> records)
        where T : Achievement =>
        Assert.Single(records, record => record.Achievement is T);
}
