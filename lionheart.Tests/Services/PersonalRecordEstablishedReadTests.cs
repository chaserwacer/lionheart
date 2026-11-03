using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services.Training.PersonalRecords;
using lionheart.Tests.Api;
using lionheart.Tests.Persistence;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Services;

/// <summary>
/// The records a movement or a window established — what marks the sets that set a record — with
/// every standing included, since a set that took a record still took it after it was beaten.
/// </summary>
public class PersonalRecordEstablishedReadTests : IAsyncLifetime
{
    private static readonly DateTime First = new(2026, 9, 3, 21, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Second = new(2026, 9, 10, 21, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Third = new(2026, 9, 17, 21, 0, 0, DateTimeKind.Utc);

    private readonly TwoAthletes _athletes = new();
    private Guid _bench;
    private Guid _squat;

    public async Task InitializeAsync()
    {
        _bench = await _athletes.PlantMovementAsync(_athletes.OwnerId, "Bench Press");
        _squat = await _athletes.PlantMovementAsync(_athletes.OwnerId, "Back Squat");
    }

    public Task DisposeAsync()
    {
        _athletes.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task A_sessions_records_are_the_window_from_its_start_to_its_start()
    {
        await LogAsync(First, new LiftSetCommand(_bench, 5, 225, WeightUnit.Pounds, null));
        await LogAsync(Second, new LiftSetCommand(_bench, 5, 245, WeightUnit.Pounds, null));

        var established = ResultAssert.Succeeded(await EstablishedAsync(new(null, Second, Second)));

        // A set of five holds the heaviest set, the best set volume, the session volume and the
        // rep maxes from two to five: seven records, every one of them this session's.
        Assert.Equal(7, established.Count);
        Assert.All(established, record => Assert.Equal(Second, record.AchievedAt));
    }

    [Fact]
    public async Task Beaten_and_retracted_records_come_back_beside_the_standing_one()
    {
        await _athletes.PlantAsync(context =>
        {
            var beaten = Heaviest(_bench, 225, First);
            var retracted = PersonalRecord.Succeeding(Guid.CreateVersion7(), Lift(245), Second, beaten);
            retracted.Retract(Third);
            context.PersonalRecords.AddRange(beaten, retracted, Heaviest(_squat, 315, Third));
        });

        var established = ResultAssert.Succeeded(await EstablishedAsync(new(_bench, null, null)));

        Assert.Equal(
            [RecordStandingView.Retracted, RecordStandingView.Beaten],
            established.Select(record => record.Standing));
    }

    [Fact]
    public async Task A_movement_filter_leaves_out_other_movements_and_trail_running()
    {
        await _athletes.PlantAsync(context => context.PersonalRecords.AddRange(
            Heaviest(_bench, 225, First),
            Heaviest(_squat, 315, First),
            PersonalRecord.FirstOf(
                Guid.CreateVersion7(), _athletes.OwnerId, new RecordSubject.TrailRunning(),
                new Achievement.LongestDistance(new DistanceEffort(8, DistanceUnit.Kilometers, TimeSpan.FromMinutes(70))),
                First)));

        var established = ResultAssert.Succeeded(await EstablishedAsync(new(_bench, null, null)));

        Assert.Equal(_bench, Assert.Single(established).MovementDataId);
    }

    [Fact]
    public async Task The_window_includes_both_its_ends_and_reads_newest_first()
    {
        await _athletes.PlantAsync(context => context.PersonalRecords.AddRange(
            Heaviest(_bench, 225, First), Heaviest(_squat, 315, Second), Heaviest(_bench, 135, Third)));

        var established = ResultAssert.Succeeded(await EstablishedAsync(new(null, First, Second)));

        Assert.Equal([Second, First], established.Select(record => record.AchievedAt));
    }

    [Fact]
    public async Task A_window_bound_at_another_offset_names_the_same_instant()
    {
        await _athletes.PlantAsync(context => context.PersonalRecords.Add(Heaviest(_bench, 225, First)));
        var eveningInNewYork = new DateTimeOffset(First).ToOffset(TimeSpan.FromHours(-4));

        var established = ResultAssert.Succeeded(await EstablishedAsync(new(null, eveningInNewYork, eveningInNewYork)));

        Assert.Single(established);
    }

    [Fact]
    public async Task Both_filters_together_narrow_to_one_movement_inside_one_window()
    {
        await _athletes.PlantAsync(context => context.PersonalRecords.AddRange(
            Heaviest(_bench, 225, First), Heaviest(_squat, 315, Second), Heaviest(_bench, 135, Second)));

        var established = ResultAssert.Succeeded(await EstablishedAsync(new(_bench, Second, Third)));

        var only = Assert.Single(established);
        Assert.Equal((_bench, Second), (only.MovementDataId!.Value, only.AchievedAt));
    }

    [Fact]
    public async Task With_no_filter_every_record_of_the_athlete_comes_back_and_no_one_elses()
    {
        var theirs = await _athletes.PlantMovementAsync(_athletes.StrangerId, "Their bench");
        await _athletes.PlantAsync(context => context.PersonalRecords.AddRange(
            Heaviest(_bench, 225, First),
            Heaviest(_squat, 315, Second),
            PersonalRecord.FirstOf(
                Guid.CreateVersion7(), _athletes.StrangerId, new RecordSubject.Movement(theirs), Lift(405), First)));

        var established = ResultAssert.Succeeded(await EstablishedAsync(new(null, null, null)));

        Assert.Equal([_squat, _bench], established.Select(record => record.MovementDataId!.Value));
    }

    [Fact]
    public async Task Another_athletes_movement_is_missing_rather_than_forbidden()
    {
        var theirs = await _athletes.PlantMovementAsync(_athletes.StrangerId, "Their bench");

        Assert.IsType<Problem.NotFound>(ResultAssert.Failed(await EstablishedAsync(new(theirs, null, null))));
        Assert.IsType<Problem.NotFound>(ResultAssert.Failed(await EstablishedAsync(new(Guid.NewGuid(), null, null))));
    }

    [Fact]
    public async Task A_window_that_ends_before_it_starts_is_refused_rather_than_answered_empty()
    {
        var refusal = Assert.IsType<Problem.Invalid>(
            ResultAssert.Failed(await EstablishedAsync(new(null, Second, First))));

        Assert.Equal("To", Assert.Single(refusal.Fields).Field);
    }

    /// <summary>
    /// Every filter has to reach SQL; one evaluated in the app still answers correctly on a few
    /// rows, so the results alone would never show it.
    /// </summary>
    [Fact]
    public void The_movement_and_the_window_narrow_in_sql()
    {
        using var context = _athletes.CreateContext();

        var sql = context.PersonalRecords
            .OnMovement(_bench)
            .AchievedFrom(First)
            .AchievedUntil(Second)
            .ToQueryString();

        Assert.Contains("\"p\".\"MovementDataId\" = @", sql);
        Assert.Contains("\"p\".\"AchievedAt\" >= @", sql);
        Assert.Contains("\"p\".\"AchievedAt\" <= @", sql);
        Assert.Contains(RemodelDatabase.SqlGuid(_bench), sql);
    }

    // ---- Fixtures ----

    private static Achievement Lift(double weight) =>
        new Achievement.HeaviestSet(new LiftPerformance(weight, WeightUnit.Pounds, 5));

    private PersonalRecord Heaviest(Guid movementDataId, double weight, DateTime achievedAt) =>
        PersonalRecord.FirstOf(
            Guid.CreateVersion7(), _athletes.OwnerId, new RecordSubject.Movement(movementDataId), Lift(weight), achievedAt);

    private Task<Result<IReadOnlyList<PersonalRecordView>, Problem>> EstablishedAsync(EstablishedRecordsQuery query) =>
        _athletes.RecordsAsync(service => service.GetEstablishedAsync(_athletes.Owner, query, default));

    private Task LogAsync(DateTime performed, params SetCommand[] sets) =>
        _athletes.SessionsAsync(service => service.LogCompletedAsync(
            _athletes.Owner,
            new LogCompletedSessionRequest(
                [.. sets], new PeriodCommand(new DateTimeOffset(performed), null), null, "logged", null),
            default));
}
