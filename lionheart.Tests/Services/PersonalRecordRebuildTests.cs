using lionheart.Functional;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services.Training.PersonalRecords;
using lionheart.Tests.Persistence;
using lionheart.Tests.Support;
using static lionheart.Tests.Support.TrainingHistory;

namespace lionheart.Tests.Services;

/// <summary>
/// Rebuilding an athlete's records from his whole history, against a real database: that it is
/// the identity on records already right, and that it puts history detected out of order back into
/// the chains a live log makes.
/// </summary>
public class PersonalRecordRebuildTests : IDisposable
{
    private static readonly DateTimeOffset January = new(2025, 1, 10, 17, 0, 0, TimeSpan.FromHours(-5));

    private readonly RemodelDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Rebuilding_records_that_are_already_right_changes_nothing_and_writes_nothing()
    {
        var squat = await _database.PlantMovementAsync("Squat", "Barbell");
        await _database.CompleteInTurnAsync(Progression(squat));
        var chains = await _database.ChainsAsync();
        var ids = (await _database.RecordsAsync()).Select(record => record.PersonalRecordId).Order().ToList();

        var report = ResultAssert.Succeeded(await RebuildAsync(_database));

        Assert.Equal(new RecordRebuildReport(ids.Count, ids.Count, ids.Count, 0, 0, 0), report);
        Assert.Equal(chains, await _database.ChainsAsync());
        Assert.Equal(ids, (await _database.RecordsAsync()).Select(record => record.PersonalRecordId).Order().ToList());
    }

    [Fact]
    public async Task History_detected_out_of_order_is_rebuilt_into_the_chains_logging_it_in_order_makes()
    {
        using var live = new RemodelDatabase();
        var liveSquat = await live.PlantMovementAsync("Squat", "Barbell");
        await live.CompleteInTurnAsync(Progression(liveSquat, live.UserId));

        // The same history, its sessions completed newest first: each older session is judged
        // against records set after it, so the chains claim later sets were beaten by earlier ones.
        var squat = await _database.PlantMovementAsync("Squat", "Barbell");
        await _database.CompleteInTurnAsync([.. Progression(squat).Reverse()]);
        Assert.NotEqual(await live.ChainsAsync(), await _database.ChainsAsync());

        var report = ResultAssert.Succeeded(await RebuildAsync(_database));

        Assert.True(report.Changed > 0);
        Assert.Equal(await live.ChainsAsync(), await _database.ChainsAsync());
    }

    [Fact]
    public async Task A_withdrawn_record_refuses_the_rebuild_and_nothing_is_written()
    {
        // The set behind it is still in its session, so a replay would award it again.
        var squat = await _database.PlantMovementAsync("Squat", "Barbell");
        await _database.CompleteInTurnAsync(Progression(squat));
        await using (var context = _database.CreateContext())
        {
            var standing = context.PersonalRecords.AsEnumerable()
                .Single(record => record.IsStanding && record.Kind is RecordKind.HeaviestSet);
            standing.Retract(January.AddYears(1).UtcDateTime);
            await context.SaveChangesAsync();
        }

        var chains = await _database.ChainsAsync();

        var problem = Assert.IsType<Problem.Conflict>(ResultAssert.Failed(await RebuildAsync(_database)));

        Assert.Contains("1 record(s) were withdrawn", problem.Description);
        Assert.Equal(chains, await _database.ChainsAsync());
    }

    [Fact]
    public async Task An_athlete_who_does_not_exist_is_refused()
    {
        await using var context = _database.CreateContext();

        var refused = await Rebuild(context).RebuildAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<Problem.NotFound>(ResultAssert.Failed(refused));
    }

    // ---- The comparison ----

    [Fact]
    public void A_record_an_older_set_now_stands_ahead_of_is_no_longer_a_record()
    {
        var squat = new RecordSubject.Movement(Guid.NewGuid());
        var newer = PersonalRecord.FirstOf(Guid.NewGuid(), Guid.NewGuid(), squat, Heaviest(300), At(2025));
        var older = PersonalRecord.FirstOf(Guid.NewGuid(), Guid.NewGuid(), squat, Heaviest(350), At(2023));

        var report = RecordRebuildReport.Between([newer], [older]);

        Assert.Equal(new RecordRebuildReport(Before: 1, After: 1, Unchanged: 0, Restated: 0, Dropped: 1, Established: 1), report);
    }

    [Fact]
    public void A_record_that_now_beats_an_older_one_is_restated_behind_it()
    {
        var squat = new RecordSubject.Movement(Guid.NewGuid());
        var athlete = Guid.NewGuid();
        var alone = PersonalRecord.FirstOf(Guid.NewGuid(), athlete, squat, Heaviest(400), At(2025));

        var older = PersonalRecord.FirstOf(Guid.NewGuid(), athlete, squat, Heaviest(350), At(2023));
        var behindIt = PersonalRecord.Succeeding(Guid.NewGuid(), Heaviest(400), At(2025), older);

        var report = RecordRebuildReport.Between([alone], [older, behindIt]);

        Assert.Equal(new RecordRebuildReport(Before: 1, After: 2, Unchanged: 0, Restated: 1, Dropped: 0, Established: 1), report);
    }

    [Fact]
    public void Records_compare_by_what_they_hold_and_not_by_id()
    {
        var squat = new RecordSubject.Movement(Guid.NewGuid());
        var athlete = Guid.NewGuid();
        var first = PersonalRecord.FirstOf(Guid.NewGuid(), athlete, squat, Heaviest(350), At(2023));
        var second = PersonalRecord.Succeeding(Guid.NewGuid(), Heaviest(400), At(2025), first);
        var firstAgain = PersonalRecord.FirstOf(Guid.NewGuid(), athlete, squat, Heaviest(350), At(2023));
        var secondAgain = PersonalRecord.Succeeding(Guid.NewGuid(), Heaviest(400), At(2025), firstAgain);

        var report = RecordRebuildReport.Between([first, second], [firstAgain, secondAgain]);

        Assert.Equal(0, report.Changed);
        Assert.Equal(2, report.Unchanged);
    }

    // ---- Helpers ----

    /// <summary>A squat progression over three months: a heavier triple, a heavier single, a lighter five.</summary>
    private CompletedTrainingSession[] Progression(Guid squat, Guid? userId = null) =>
    [
        Lifts(userId ?? _database.UserId, January, squat, (300, 3), (285, 5)),
        Lifts(userId ?? _database.UserId, January.AddMonths(1), squat, (315, 3)),
        Lifts(userId ?? _database.UserId, January.AddMonths(2), squat, (335, 1), (275, 8)),
    ];

    private static Achievement Heaviest(double weight) =>
        new Achievement.HeaviestSet(new LiftPerformance(weight, WeightUnit.Pounds, 1));

    private static DateTime At(int year) => new(year, 3, 1, 17, 0, 0, DateTimeKind.Utc);

    private static async Task<Result<RecordRebuildReport, Problem>> RebuildAsync(RemodelDatabase database)
    {
        await using var context = database.CreateContext();
        return await Rebuild(context).RebuildAsync(database.UserId, CancellationToken.None);
    }

    private static PersonalRecordRebuild Rebuild(lionheart.Data.ModelContext context) =>
        new(context, new PersonalRecordDetector(context, TimeProvider.System));
}
