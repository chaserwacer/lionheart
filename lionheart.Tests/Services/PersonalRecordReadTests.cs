using lionheart.Functional;
using lionheart.Model.Training;
using lionheart.Model.Analysis;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services.Training.PersonalRecords;
using lionheart.Tests.Api;
using lionheart.Tests.Support;

namespace lionheart.Tests.Services;

/// <summary>
/// The read surface both clients consume: what stands, grouped and flat, and one subject's whole
/// progression.
/// </summary>
public class PersonalRecordReadTests : IAsyncLifetime
{
    private static readonly DateTimeOffset First = new(2026, 9, 3, 17, 0, 0, TimeSpan.FromHours(-4));
    private static readonly DateTimeOffset Second = new(2026, 9, 10, 17, 0, 0, TimeSpan.FromHours(-4));

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
    public async Task An_account_with_no_profile_yet_reads_empty_rather_than_being_refused()
    {
        // The rule every read in this domain follows: writes refuse an unprofiled account, reads
        // answer empty, so a records screen says "no records yet" instead of a conflict the
        // athlete cannot act on from there.
        var standing = ResultAssert.Succeeded(await _athletes.RecordsAsync(service =>
            service.GetStandingAsync(_athletes.Unprofiled, default)));

        Assert.Empty(standing);
    }

    [Fact]
    public async Task A_movement_the_athlete_has_never_set_a_record_on_answers_an_empty_summary()
    {
        // Not a 404: the movement exists and simply holds nothing, which a screen states as "no
        // records yet" rather than as a missing movement.
        var summary = ResultAssert.Succeeded(await _athletes.RecordsAsync(service =>
            service.GetForMovementAsync(_athletes.Owner, _bench, default)));

        Assert.Equal(_bench, summary.MovementDataId);
        Assert.Empty(summary.Records);
        Assert.Null(summary.LastAchievedAt);
    }

    [Fact]
    public async Task Another_athletes_movement_is_missing_rather_than_forbidden()
    {
        var theirs = await _athletes.PlantMovementAsync(_athletes.StrangerId, "Their bench");

        var refusal = ResultAssert.Failed(await _athletes.RecordsAsync(service =>
            service.GetForMovementAsync(_athletes.Owner, theirs, default)));

        Assert.IsType<Problem.NotFound>(refusal);
    }

    [Fact]
    public async Task A_summary_carries_every_kind_the_movement_holds_with_its_rep_grid_in_rep_order()
    {
        await LogAsync(First, new LiftSetCommand(_bench, 5, 225, WeightUnit.Pounds, null));
        await LogAsync(Second, new LiftSetCommand(_bench, 3, 255, WeightUnit.Pounds, null));

        var summary = ResultAssert.Succeeded(await _athletes.RecordsAsync(service =>
            service.GetForMovementAsync(_athletes.Owner, _bench, default)));

        Assert.Equal(Second.UtcDateTime, summary.LastAchievedAt);
        Assert.Equal(
            [3, 5],
            summary.Records
                .Select(record => record.Achievement)
                .OfType<RepMaxView>()
                .Select(grid => grid.Reps));
    }

    [Fact]
    public async Task The_grouped_read_separates_trail_running_from_the_movements()
    {
        await LogAsync(First, new LiftSetCommand(_bench, 5, 225, WeightUnit.Pounds, null));
        await LogAsync(
            Second,
            new TrailRunSetCommand(8, DistanceUnit.Kilometers, TimeSpan.FromMinutes(70), 600, null));

        var grouped = ResultAssert.Succeeded(await _athletes.RecordsAsync(service =>
            service.GetBySubjectAsync(_athletes.Owner, default)));

        Assert.Equal(2, grouped.Count);
        Assert.Contains(grouped, subject => subject.MovementDataId is null);
        Assert.Contains(grouped, subject => subject.MovementDataId == _bench);
    }

    [Fact]
    public async Task A_history_read_shows_the_beaten_link_as_well_as_the_standing_one()
    {
        await LogAsync(First, new LiftSetCommand(_bench, 5, 225, WeightUnit.Pounds, null));
        await LogAsync(Second, new LiftSetCommand(_bench, 5, 245, WeightUnit.Pounds, null));

        var chain = ResultAssert.Succeeded(await HistoryAsync("heaviestSet", null));

        Assert.Equal(2, chain.Count);
        Assert.Equal(
            [RecordStandingView.Standing, RecordStandingView.Beaten],
            chain.Select(record => record.Standing));
        Assert.Equal(First.UtcDateTime, chain[0].PreviousAchievedAt);
    }

    [Fact]
    public async Task A_history_read_names_the_rep_max_it_wants()
    {
        await LogAsync(First, new LiftSetCommand(_bench, 5, 225, WeightUnit.Pounds, null));
        await LogAsync(First, new LiftSetCommand(_bench, 3, 255, WeightUnit.Pounds, null));

        var fives = ResultAssert.Succeeded(await HistoryAsync("repMax", 5));

        Assert.Equal(5, Assert.IsType<RepMaxView>(Assert.Single(fives).Achievement).Reps);
    }

    [Fact]
    public async Task A_history_read_without_a_rep_count_is_refused_rather_than_guessed() =>
        Assert.IsType<Problem.Invalid>(ResultAssert.Failed(await HistoryAsync("repMax", null)));

    [Fact]
    public async Task A_history_read_for_a_kind_nobody_defined_is_refused() =>
        Assert.IsType<Problem.Invalid>(ResultAssert.Failed(await HistoryAsync("mostSetsEver", null)));

    [Fact]
    public async Task A_history_read_with_no_movement_asks_about_trail_running()
    {
        await LogAsync(
            First,
            new TrailRunSetCommand(8, DistanceUnit.Kilometers, TimeSpan.FromMinutes(70), 600, null));

        var trail = ResultAssert.Succeeded(await _athletes.RecordsAsync(service =>
            service.GetHistoryAsync(
                _athletes.Owner, new RecordHistoryQuery(null, "longestDistance", null), default)));

        Assert.Null(Assert.Single(trail).MovementDataId);
    }

    /// <summary>
    /// Two links of one chain at one instant read successor first.
    /// </summary>
    /// <remarks>
    /// One commit of two sessions with the same period writes exactly this. The ids are chosen so
    /// the successor's sorts below its predecessor's: a version-7 id is random within a
    /// millisecond, and ordering on it put the beaten record on top about a third of the time, so
    /// pinning the unlucky draw is what makes this fail every time without the fix rather than
    /// sometimes.
    /// </remarks>
    [Fact]
    public async Task Two_links_of_a_chain_at_one_instant_read_with_the_successor_first()
    {
        var predecessorId = Guid.Parse("ffffffff-ffff-7fff-bfff-ffffffffffff");
        var successorId = Guid.Parse("00000000-0000-7000-8000-000000000001");

        await _athletes.PlantAsync(context =>
        {
            var predecessor = PersonalRecord.FirstOf(
                predecessorId,
                _athletes.OwnerId,
                new RecordSubject.Movement(_bench),
                new Achievement.HeaviestSet(new LiftPerformance(225, WeightUnit.Pounds, 5)),
                First.UtcDateTime);
            var successor = PersonalRecord.Succeeding(
                successorId,
                new Achievement.HeaviestSet(new LiftPerformance(245, WeightUnit.Pounds, 5)),
                First.UtcDateTime,
                predecessor);
            context.PersonalRecords.AddRange(predecessor, successor);
        });

        var chain = ResultAssert.Succeeded(await HistoryAsync("heaviestSet", null));

        Assert.Equal([successorId, predecessorId], chain.Select(record => record.PersonalRecordId));
        Assert.Equal(
            [RecordStandingView.Standing, RecordStandingView.Beaten],
            chain.Select(record => record.Standing));
    }

    [Fact]
    public async Task A_one_commit_pair_of_same_instant_sessions_reads_its_chain_in_the_order_it_happened()
    {
        // The realistic route to the same state: one detector call staging two sessions with one
        // period, so both ids come out of the same millisecond. Two separate requests would not
        // do — their ids land in different milliseconds and happen to sort by write order. Run
        // enough times that a random tie-break would all but certainly lose at least once.
        for (var attempt = 0; attempt < 20; attempt++)
        {
            using var fresh = new TwoAthletes();
            var bench = await fresh.PlantMovementAsync(fresh.OwnerId, "Bench Press");

            using (var context = fresh.CreateContext())
            {
                await new PersonalRecordDetector(context, TimeProvider.System).StageAsync(
                    [SessionOf(fresh.OwnerId, bench, 225), SessionOf(fresh.OwnerId, bench, 245)], default);
                await context.SaveChangesAsync();
            }

            var chain = ResultAssert.Succeeded(await fresh.RecordsAsync(service => service.GetHistoryAsync(
                fresh.Owner, new RecordHistoryQuery(bench, "heaviestSet", null), default)));

            Assert.Equal(
                [245d, 225d],
                chain.Select(record => Assert.IsType<HeaviestSetView>(record.Achievement).Weight));
        }
    }

    private static CompletedTrainingSession SessionOf(Guid userId, Guid movementDataId, double weight) =>
        new(
            [new LiftSet(movementDataId, 5, weight, WeightUnit.Pounds, Rpe.Unrated) { Source = new Formed(), Facets = [] }],
            new Instant(First),
            new EffortNotReported(),
            new NotDecorated())
        {
            TrainingSessionId = Guid.NewGuid(),
            UserId = userId,
            Notes = string.Empty,
            Provenance = Provenance.User,
            Membership = new Standalone(),
            CreatedAt = Some.Clock,
        };

    private Task<Result<IReadOnlyList<PersonalRecordView>, Problem>> HistoryAsync(
        string kind, int? bracket) =>
        _athletes.RecordsAsync(service => service.GetHistoryAsync(
            _athletes.Owner, new RecordHistoryQuery(_bench, kind, bracket), default));

    private Task LogAsync(DateTimeOffset performed, params SetCommand[] sets) =>
        _athletes.SessionsAsync(service => service.LogCompletedAsync(
            _athletes.Owner,
            new LogCompletedSessionRequest(
                [.. sets], new PeriodCommand(performed, null), null, "logged", null),
            default));
}
