using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Training.Availability;
using lionheart.Model.Training.Sessions;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Api;

/// <summary>
/// Non-training days through the real schema: marked, listed, read back as availability beside the
/// sessions planned, removed whole, refused when malformed or repeated, and never another athlete's.
/// </summary>
public sealed class NonTrainingDayServiceTests : IDisposable
{
    /// <summary>A Sunday.</summary>
    private static readonly DateOnly Sunday = new(2026, 11, 8);

    private static readonly FixedClock Clock = new(new DateTimeOffset(2026, 11, 4, 12, 30, 0, TimeSpan.Zero));

    private readonly TwoAthletes _athletes = new();

    public void Dispose() => _athletes.Dispose();

    private Task<T> DaysAsync<T>(Func<lionheart.Services.Training.Availability.INonTrainingDayService, Task<T>> act) =>
        _athletes.NonTrainingDaysAsync(Clock, act);

    private Task<Result<NonTrainingDayView, Problem>> MarkAsync(MarkNonTrainingDayRequest request) =>
        DaysAsync(service => service.MarkAsync(_athletes.Owner, request, default));

    private async Task<int> StoredAsync()
    {
        using var context = _athletes.CreateContext();
        return await context.NonTrainingDays.CountAsync();
    }

    [Fact]
    public async Task A_weekly_day_with_a_last_day_and_a_note_round_trips_whole()
    {
        var marked = ResultAssert.Succeeded(await MarkAsync(
            new MarkNonTrainingDayRequest(Sunday, NonTrainingRepeat.Weekly, Sunday.AddDays(28), "  family lunch ")));

        var listed = Assert.Single(ResultAssert.Succeeded(await DaysAsync(service => service.GetAllAsync(_athletes.Owner, default))));
        Assert.Equal(marked, listed);
        var schedule = Assert.IsType<NonTrainingSchedule.EveryWeekUntil>(listed.Schedule);
        Assert.Equal((Sunday, Sunday.AddDays(28)), (schedule.From, schedule.Until));
        Assert.Equal(("family lunch", Clock.GetUtcNow().UtcDateTime), (listed.Note, listed.MarkedAt));
    }

    [Fact]
    public async Task Availability_reads_the_days_marked_beside_the_sessions_planned()
    {
        ResultAssert.Succeeded(await MarkAsync(new MarkNonTrainingDayRequest(Sunday.AddDays(-7), NonTrainingRepeat.Weekly)));
        ResultAssert.Succeeded(await _athletes.SessionsAsync(service => service.PlanAsync(
            _athletes.Owner, new PlanSessionRequest(Sunday, [], "", null), default)));

        var calendar = ResultAssert.Succeeded(await DaysAsync(service =>
            service.GetAvailabilityAsync(_athletes.Owner, new DayRange(Sunday.AddDays(-7), Sunday), default)));

        Assert.IsType<DayAvailability.NonTraining>(calendar.Days[0]);
        Assert.IsType<DayAvailability.Planned>(calendar.Days[^1]);
        Assert.Equal(7, calendar.AvailableDays);
    }

    [Fact]
    public async Task A_day_trained_reads_as_trained_though_it_was_marked_off()
    {
        ResultAssert.Succeeded(await MarkAsync(new MarkNonTrainingDayRequest(Sunday, Note: "travel")));
        var bench = await _athletes.PlantMovementAsync(_athletes.OwnerId, "Bench Press");
        // An evening west of Greenwich, already Monday in UTC: the session is the athlete's Sunday.
        await _athletes.PlantAsync(context => context.Sessions.Add(TrainingHistory.Lifts(
            _athletes.OwnerId, new DateTimeOffset(Sunday.ToDateTime(new TimeOnly(21, 0)), TimeSpan.FromHours(-5)), bench, (135, 5))));

        var calendar = ResultAssert.Succeeded(await DaysAsync(service =>
            service.GetAvailabilityAsync(_athletes.Owner, new DayRange(Sunday.AddDays(-1), Sunday.AddDays(1)), default)));

        Assert.Equal(
            [new DayAvailability.Open(Sunday.AddDays(-1)), new DayAvailability.Trained(Sunday), new DayAvailability.Open(Sunday.AddDays(1))],
            calendar.Days);
    }

    [Fact]
    public async Task A_request_without_its_day_is_refused_naming_day_and_writes_nothing()
    {
        var problem = ResultAssert.Failed(await MarkAsync(new MarkNonTrainingDayRequest(null, NonTrainingRepeat.Weekly)));

        Assert.Equal("Day", Assert.Single(Assert.IsType<Problem.Invalid>(problem).Fields).Field);
        Assert.Equal(0, await StoredAsync());
    }

    [Fact]
    public async Task Removing_a_weekly_day_frees_every_day_it_covered()
    {
        var marked = ResultAssert.Succeeded(await MarkAsync(new MarkNonTrainingDayRequest(Sunday, NonTrainingRepeat.Weekly)));

        ResultAssert.Succeeded(await DaysAsync(service => service.RemoveAsync(_athletes.Owner, marked.NonTrainingDayId, default)));

        Assert.Equal(0, await StoredAsync());
    }

    [Fact]
    public async Task The_same_schedule_marked_twice_is_a_conflict_and_writes_nothing_more()
    {
        ResultAssert.Succeeded(await MarkAsync(new MarkNonTrainingDayRequest(Sunday)));

        Assert.IsType<Problem.Conflict>(ResultAssert.Failed(await MarkAsync(new MarkNonTrainingDayRequest(Sunday, Note: "again"))));
        Assert.Equal(1, await StoredAsync());
    }

    [Fact]
    public async Task A_malformed_day_is_refused_naming_its_field_and_writes_nothing()
    {
        var problem = ResultAssert.Failed(await MarkAsync(new MarkNonTrainingDayRequest(Sunday, NonTrainingRepeat.Once, Sunday.AddDays(7))));

        Assert.Equal("Until", Assert.Single(Assert.IsType<Problem.Invalid>(problem).Fields).Field);
        Assert.Equal(0, await StoredAsync());
    }

    [Fact]
    public async Task Another_athletes_day_is_not_found_and_not_listed()
    {
        var marked = ResultAssert.Succeeded(await MarkAsync(new MarkNonTrainingDayRequest(Sunday)));

        var removal = await DaysAsync(service => service.RemoveAsync(_athletes.Stranger, marked.NonTrainingDayId, default));
        var theirs = ResultAssert.Succeeded(await DaysAsync(service => service.GetAllAsync(_athletes.Stranger, default)));

        Assert.IsType<Problem.NotFound>(ResultAssert.Failed(removal));
        Assert.Empty(theirs);
        Assert.Equal(1, await StoredAsync());
    }

    [Fact]
    public async Task An_availability_range_past_a_year_and_a_week_is_refused_naming_to()
    {
        var problem = ResultAssert.Failed(await DaysAsync(service =>
            service.GetAvailabilityAsync(_athletes.Owner, new DayRange(Sunday, Sunday.AddDays(371)), default)));

        Assert.Equal("To", Assert.Single(Assert.IsType<Problem.Invalid>(problem).Fields).Field);
    }
}
