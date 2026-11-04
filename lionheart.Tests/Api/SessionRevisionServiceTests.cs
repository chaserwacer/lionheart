using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Api;

/// <summary>
/// Correcting a completed session after the fact, and the records that have to stay true when the
/// sets or the day change underneath them.
/// </summary>
public class SessionRevisionServiceTests : IAsyncLifetime
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
    public async Task A_revision_restates_the_sets_time_and_notes_and_keeps_everything_else()
    {
        var logged = await LogAsync(First, 225);
        var moved = First.AddDays(-1);

        var revised = Assert.IsType<CompletedSessionView>(ResultAssert.Succeeded(await ReviseAsync(
            logged.TrainingSessionId,
            [Lift(235, 5), Lift(235, 4)],
            new PeriodCommand(moved, moved.AddMinutes(50)),
            "corrected")));

        Assert.Equal(logged.TrainingSessionId, revised.TrainingSessionId);
        Assert.Equal(2, revised.Sets.Count);
        Assert.Equal(new Interval(moved, moved.AddMinutes(50)), revised.Period);
        Assert.Equal("corrected", revised.Notes);
        Assert.Equal(logged.Effort, revised.Effort);
        Assert.Equal(logged.CreatedAt, revised.CreatedAt);
        using var context = _athletes.CreateContext();
        Assert.Single(await context.Sessions.ToListAsync());
    }

    [Theory]
    [InlineData("planned")]
    [InlineData("active")]
    [InlineData("skipped")]
    public async Task Only_a_completed_session_can_be_revised(string lifecycle)
    {
        var session = lifecycle switch
        {
            "planned" => (Model.Training.Sessions.TrainingSession)Some.PlannedSession(_athletes.OwnerId),
            "active" => Some.ActiveSession(_athletes.OwnerId),
            _ => Some.SkippedSession(_athletes.OwnerId),
        };
        await _athletes.PlantAsync(context => context.Sessions.Add(session));

        var refusal = ResultAssert.Failed(await ReviseAsync(
            session.TrainingSessionId, [Lift(225, 5)], new PeriodCommand(First, null), ""));

        Assert.Contains("only a completed session", Assert.IsType<Problem.Conflict>(refusal).What);
    }

    [Fact]
    public async Task Lowering_a_mistyped_top_set_hands_the_record_to_the_set_that_really_was_heaviest()
    {
        var mistyped = await LogAsync(First, 325);
        await LogAsync(Second, 245);

        ResultAssert.Succeeded(await ReviseAsync(
            mistyped.TrainingSessionId, [Lift(225, 5)], new PeriodCommand(First, First.AddHours(1)), ""));

        var heaviest = await HeaviestAsync();
        Assert.Equal([225d, 245d], heaviest.Select(record => record.Weight));
        Assert.True(heaviest.Last().IsStanding);
        Assert.False(heaviest.First().IsStanding);
    }

    [Fact]
    public async Task Moving_a_session_earlier_reorders_the_chain_it_belongs_to()
    {
        await LogAsync(First, 225);
        var later = await LogAsync(Second, 245);
        var earlier = First.AddDays(-7);

        ResultAssert.Succeeded(await ReviseAsync(
            later.TrainingSessionId, [Lift(245, 5)], new PeriodCommand(earlier, earlier.AddHours(1)), ""));

        // The 245 now came first, so the 225 a week later never beat anything and is no record.
        var heaviest = await HeaviestAsync();
        var only = Assert.Single(heaviest);
        Assert.Equal(245d, only.Weight);
        Assert.True(only.IsStanding);
    }

    [Fact]
    public async Task A_notes_only_correction_leaves_the_records_exactly_as_they_were()
    {
        var logged = await LogAsync(First, 225);
        var before = await RecordIdsAsync();

        ResultAssert.Succeeded(await ReviseAsync(
            logged.TrainingSessionId, [Lift(225, 5)], new PeriodCommand(First, First.AddHours(1)), "felt heavy"));

        Assert.Equal(before, await RecordIdsAsync());
    }

    [Fact]
    public async Task A_withdrawn_record_refuses_a_change_to_the_sets_and_nothing_is_written()
    {
        var logged = await LogAsync(First, 225);
        await WithdrawStandingHeaviestAsync();
        var before = await RecordIdsAsync();

        var refusal = ResultAssert.Failed(await ReviseAsync(
            logged.TrainingSessionId, [Lift(235, 5)], new PeriodCommand(First, First.AddHours(1)), "changed"));

        Assert.Contains("withdrawn", Assert.IsType<Problem.Conflict>(refusal).What);
        Assert.Equal(before, await RecordIdsAsync());
        var stored = Assert.IsType<CompletedSessionView>(ResultAssert.Succeeded(await _athletes.SessionsAsync(
            service => service.GetAsync(_athletes.Owner, logged.TrainingSessionId, default))));
        Assert.Equal(logged.Notes, stored.Notes);
        Assert.Equal(225d, Assert.IsType<LiftSet>(Assert.Single(stored.Sets)).Weight);
    }

    [Fact]
    public async Task A_withdrawn_record_does_not_stand_in_the_way_of_a_notes_only_correction()
    {
        var logged = await LogAsync(First, 225);
        await WithdrawStandingHeaviestAsync();

        var revised = ResultAssert.Succeeded(await ReviseAsync(
            logged.TrainingSessionId, [Lift(225, 5)], new PeriodCommand(First, First.AddHours(1)), "fine"));

        Assert.Equal("fine", revised.Notes);
    }

    [Fact]
    public async Task A_change_to_the_sets_takes_the_analysis_projection_of_the_old_ones_with_it()
    {
        var logged = await LogAsync(First, 225);
        await _athletes.PlantAsync(context => context.AnalyzableEntries.Add(
            Some.AnalyzableEntry(_athletes.OwnerId) with { EntryId = logged.TrainingSessionId }));

        ResultAssert.Succeeded(await ReviseAsync(
            logged.TrainingSessionId, [Lift(235, 5)], new PeriodCommand(First, First.AddHours(1)), ""));

        using var context = _athletes.CreateContext();
        Assert.Empty(await context.AnalyzableEntries.ToListAsync());
    }

    [Fact]
    public async Task Another_athletes_session_is_missing_rather_than_forbidden()
    {
        var logged = await LogAsync(First, 225);

        var refusal = ResultAssert.Failed(await _athletes.SessionsAsync(service => service.ReviseAsync(
            _athletes.Stranger,
            logged.TrainingSessionId,
            new ReviseSessionRequest([Lift(235, 5)], new PeriodCommand(First, null), ""),
            default)));

        Assert.IsType<Problem.NotFound>(refusal);
    }

    [Fact]
    public async Task An_invalid_set_is_refused_before_anything_is_written()
    {
        var logged = await LogAsync(First, 225);
        var before = await RecordIdsAsync();

        var refusal = ResultAssert.Failed(await ReviseAsync(
            logged.TrainingSessionId,
            [new LiftSetCommand(_bench, 5, 235, WeightUnit.Pounds, 11)],
            new PeriodCommand(First, null),
            ""));

        Assert.Equal("Rpe", Assert.Single(Assert.IsType<Problem.Invalid>(refusal).Fields).Field);
        Assert.Equal(before, await RecordIdsAsync());
    }

    // ---- Fixtures ----

    private LiftSetCommand Lift(double weight, int reps) => new(_bench, reps, weight, WeightUnit.Pounds, null);

    private async Task<CompletedSessionView> LogAsync(DateTimeOffset start, double weight) =>
        Assert.IsType<CompletedSessionView>(ResultAssert.Succeeded(await _athletes.SessionsAsync(service =>
            service.LogCompletedAsync(
                _athletes.Owner,
                new LogCompletedSessionRequest(
                    [Lift(weight, 5)], new PeriodCommand(start, start.AddHours(1)), null, "as logged", null, null),
                default))));

    private Task<Result<TrainingSessionView, Problem>> ReviseAsync(
        Guid sessionId, IReadOnlyList<SetCommand> sets, PeriodCommand period, string notes) =>
        _athletes.SessionsAsync(service => service.ReviseAsync(
            _athletes.Owner, sessionId, new ReviseSessionRequest(sets, period, notes), default));

    /// <summary>The heaviest-set chain on the bench, oldest first, as weight and standing.</summary>
    private async Task<IReadOnlyList<(double Weight, bool IsStanding)>> HeaviestAsync()
    {
        using var context = _athletes.CreateContext();
        var records = await context.PersonalRecords.AsNoTracking().ToListAsync();
        return
        [
            .. records
                .Where(record => record.Achievement is Achievement.HeaviestSet)
                .OrderBy(record => record.AchievedAt)
                .Select(record => (((Achievement.HeaviestSet)record.Achievement).Lift.Weight, record.IsStanding)),
        ];
    }

    private async Task<IReadOnlyList<Guid>> RecordIdsAsync()
    {
        using var context = _athletes.CreateContext();
        return [.. (await context.PersonalRecords.AsNoTracking().ToListAsync()).Select(record => record.PersonalRecordId).Order()];
    }

    private async Task WithdrawStandingHeaviestAsync()
    {
        using var context = _athletes.CreateContext();
        var standing = context.PersonalRecords.AsEnumerable()
            .Single(record => record.IsStanding && record.Kind is RecordKind.HeaviestSet);
        standing.Retract(Second.AddDays(30).UtcDateTime);
        await context.SaveChangesAsync();
    }
}
