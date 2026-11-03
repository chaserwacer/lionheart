using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Tests.Api;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Services;

/// <summary>
/// Withdrawing a record the athlete says was not real, and what that leaves standing.
/// </summary>
/// <remarks>
/// The record is the only thing in the training domain an athlete edits after the fact, because a
/// completed session is terminal: a mistyped weight cannot be corrected by re-completing the
/// session it came from.
/// </remarks>
public class PersonalRecordRetractionTests : IAsyncLifetime
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
    public async Task Retracting_a_record_puts_the_one_it_beat_back()
    {
        await LogAsync(First, 225);
        await LogAsync(Second, 245);

        var outcome = ResultAssert.Succeeded(await RetractAsync(await StandingHeaviestIdAsync()));

        var restored = Assert.IsType<RetractionOutcome.Restored>(outcome).Record;
        Assert.Equal(
            225,
            Assert.IsType<HeaviestSetView>(restored.Achievement).Weight);
        Assert.Equal(RecordStandingView.Standing, restored.Standing);
    }

    [Fact]
    public async Task A_withdrawn_record_reads_as_withdrawn_rather_than_as_beaten()
    {
        // The whole reason the standing is three states and not a flag: a number the athlete said
        // was never real must not read as progress they made.
        await LogAsync(First, 225);
        await LogAsync(Second, 245);
        var withdrawn = await StandingHeaviestIdAsync();

        ResultAssert.Succeeded(await RetractAsync(withdrawn));

        using var context = _athletes.CreateContext();
        var record = await context.PersonalRecords.FindAsync(withdrawn);
        Assert.IsType<RecordStanding.Retracted>(record!.Standing);
    }

    [Fact]
    public async Task Retracting_the_only_record_of_its_kind_leaves_the_subject_holding_none()
    {
        await LogAsync(First, 225);

        var outcome = ResultAssert.Succeeded(await RetractAsync(await StandingHeaviestIdAsync()));

        Assert.IsType<RetractionOutcome.NothingStands>(outcome);
        Assert.DoesNotContain(
            ResultAssert.Succeeded(await _athletes.RecordsAsync(service =>
                service.GetStandingAsync(_athletes.Owner, default))),
            record => record.Achievement is HeaviestSetView);
    }

    [Fact]
    public async Task Retracting_twice_over_collides_with_the_state_it_left_the_record_in()
    {
        // The same request was legitimate a moment earlier, which is a conflict rather than a
        // malformed ask.
        await LogAsync(First, 225);
        var record = await StandingHeaviestIdAsync();
        ResultAssert.Succeeded(await RetractAsync(record));

        Assert.IsType<Problem.Conflict>(ResultAssert.Failed(await RetractAsync(record)));
    }

    [Fact]
    public async Task A_record_the_athlete_already_beat_is_history_and_cannot_be_withdrawn()
    {
        await LogAsync(First, 225);
        await LogAsync(Second, 245);

        using var context = _athletes.CreateContext();
        var beaten = await context.PersonalRecords
            .AsNoTracking()
            .Where(r => r.UserId == _athletes.OwnerId)
            .ToListAsync();
        var beatenId = beaten
            .Single(r => !r.IsStanding && r.Achievement is Achievement.HeaviestSet)
            .PersonalRecordId;

        Assert.IsType<Problem.Conflict>(ResultAssert.Failed(await RetractAsync(beatenId)));
    }

    [Fact]
    public async Task Another_athletes_record_is_missing_rather_than_forbidden()
    {
        await LogAsync(First, 225);
        var mine = await StandingHeaviestIdAsync();

        var refusal = ResultAssert.Failed(await _athletes.RecordsAsync(service =>
            service.RetractAsync(_athletes.Stranger, mine, default)));

        Assert.IsType<Problem.NotFound>(refusal);
    }

    [Fact]
    public async Task A_record_that_does_not_exist_is_not_found() =>
        Assert.IsType<Problem.NotFound>(ResultAssert.Failed(await RetractAsync(Guid.NewGuid())));

    [Fact]
    public async Task After_a_retraction_the_restored_record_can_be_beaten_again()
    {
        // Which is the point of restoring it rather than deleting the chain: the 225 is the number
        // to beat again.
        await LogAsync(First, 225);
        await LogAsync(Second, 245);
        ResultAssert.Succeeded(await RetractAsync(await StandingHeaviestIdAsync()));

        await LogAsync(Second.AddDays(7), 235);

        var standing = ResultAssert.Succeeded(await _athletes.RecordsAsync(service =>
            service.GetStandingAsync(_athletes.Owner, default)));

        Assert.Equal(
            235,
            Assert.IsType<HeaviestSetView>(
                standing.Single(record => record.Achievement is HeaviestSetView).Achievement).Weight);
    }

    private Task<Result<RetractionOutcome, Problem>> RetractAsync(Guid personalRecordId) =>
        _athletes.RecordsAsync(service =>
            service.RetractAsync(_athletes.Owner, personalRecordId, default));

    private async Task<Guid> StandingHeaviestIdAsync()
    {
        using var context = _athletes.CreateContext();
        var mine = await context.PersonalRecords
            .AsNoTracking()
            .Where(record => record.UserId == _athletes.OwnerId)
            .ToListAsync();

        return mine
            .Single(record => record.IsStanding && record.Achievement is Achievement.HeaviestSet)
            .PersonalRecordId;
    }

    private Task LogAsync(DateTimeOffset performed, double weight) =>
        _athletes.SessionsAsync(service => service.LogCompletedAsync(
            _athletes.Owner,
            new LogCompletedSessionRequest(
                [new LiftSetCommand(_bench, 5, weight, WeightUnit.Pounds, null)],
                new PeriodCommand(performed, null),
                null,
                "logged",
                null),
            default));
}
