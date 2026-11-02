using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Oura;
using lionheart.Services.Oura;
using lionheart.Services.Oura.Import;
using lionheart.Tests.Persistence;
using lionheart.Tests.Support;
using Microsoft.AspNetCore.Identity;

namespace lionheart.Tests.Oura;

/// <summary>
/// The read side, over a database loaded from the recorded archive — so what these assert is what
/// the iOS client would actually receive for the athlete's own data.
/// </summary>
public class OuraReadServiceTests : IAsyncLifetime
{
    private readonly RemodelDatabase _database = new();
    private IdentityUser _user = null!;

    public async Task InitializeAsync()
    {
        await using var context = _database.CreateContext();
        _user = new IdentityUser("tester@lionheart.local") { Id = _database.UserId.ToString() };

        await new OuraArchiveImporter(context)
            .ImportAsync(RecordedPayloads.ArchiveDirectory, _database.UserId, CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        _database.Dispose();
        return Task.CompletedTask;
    }

    // ---- Days ----

    [Fact]
    public async Task A_day_answers_with_whichever_collections_the_ring_recorded_for_it()
    {
        // 2023-01-06 is a day the archive has readiness, daily sleep and SpO2 for but no activity
        // document: activity's first archived day is the 5th. The absences are what the shape is
        // for — a readiness score with no activity beside it is a real day, not a broken one.
        var day = ResultAssert.Succeeded(
            await Read().GetDayAsync(_user, new DateOnly(2023, 1, 6), CancellationToken.None));

        Assert.Equal(new DateOnly(2023, 1, 6), day.Day);
        Assert.Equal(80, day.Readiness?.Score);
        Assert.Equal(58, day.Sleep?.Score);
        Assert.NotNull(day.Spo2);
        Assert.Null(day.Activity);
        Assert.Null(day.Stress);
        Assert.Null(day.Resilience);
    }

    [Fact]
    public async Task A_day_the_ring_recorded_nothing_for_is_not_found_rather_than_empty()
    {
        // An empty panel would read as "the ring recorded nothing that day" when the honest answer
        // is "there is no such day here at all" — the same distinction TimeWindow.Ordered draws.
        var refused = await Read().GetDayAsync(_user, new DateOnly(1999, 1, 1), CancellationToken.None);

        var problem = ResultAssert.Failed(refused);
        Assert.IsType<Problem.NotFound>(problem);
        Assert.Contains("1999-01-01", problem.Description);
    }

    [Fact]
    public async Task A_range_answers_one_entry_per_day_any_collection_covered_in_date_order()
    {
        var days = ResultAssert.Succeeded(await Read().GetDaysAsync(
            _user, new DayRange(new DateOnly(2023, 1, 1), new DateOnly(2023, 1, 31)), CancellationToken.None));

        Assert.Equal(
            [new DateOnly(2023, 1, 5), new DateOnly(2023, 1, 6), new DateOnly(2023, 1, 7)],
            days.Select(day => day.Day));
        Assert.NotNull(days[0].Activity);
        Assert.Null(days[0].Readiness);
    }

    [Fact]
    public async Task A_day_Oura_issued_two_documents_for_answers_with_one_of_them_consistently()
    {
        // Oura sent two SpO2 documents a day for 128 consecutive days. Both are stored, because a
        // day is not that collection's key; a day read takes one, in document-id order, so the
        // same call does not answer differently each time.
        var first = ResultAssert.Succeeded(
            await Read().GetDayAsync(_user, new DateOnly(2024, 11, 1), CancellationToken.None));
        var again = ResultAssert.Succeeded(
            await Read().GetDayAsync(_user, new DateOnly(2024, 11, 1), CancellationToken.None));

        Assert.Equal(98.374, first.Spo2?.AverageSpo2Percentage);
        Assert.Equal(first.Spo2, again.Spo2);
    }

    [Fact]
    public async Task A_backwards_range_is_refused_rather_than_answered_with_nothing()
    {
        var refused = await Read().GetDaysAsync(
            _user, new DayRange(new DateOnly(2023, 2, 1), new DateOnly(2023, 1, 1)), CancellationToken.None);

        Assert.IsType<Problem.Invalid>(ResultAssert.Failed(refused));
    }

    [Fact]
    public async Task Another_athletes_days_are_not_returned()
    {
        var stranger = new IdentityUser("stranger@lionheart.local") { Id = Guid.NewGuid().ToString() };

        var days = ResultAssert.Succeeded(await Read().GetDaysAsync(
            stranger, new DayRange(new DateOnly(2023, 1, 1), new DateOnly(2026, 12, 31)), CancellationToken.None));

        Assert.Empty(days);
    }

    // ---- Events ----

    [Fact]
    public async Task Sleep_periods_come_back_windowed_on_the_clock_in_order()
    {
        var window = new TimeWindow(
            new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2023, 2, 1, 0, 0, 0, TimeSpan.Zero));

        var nights = ResultAssert.Succeeded(await Read().GetSleepAsync(_user, window, CancellationToken.None));

        // The archive's third sleep record is filed under 2023-01-08 but began on the 7th, so a
        // window on the clock orders it before the night filed under the 6th would suggest.
        Assert.Equal(2, nights.Count);
        Assert.Equal(new DateOnly(2023, 1, 6), nights[0].Day);
        Assert.Equal(new DateOnly(2023, 1, 8), nights[1].Day);
        Assert.True(nights[0].Period.StartTimeUtc < nights[1].Period.StartTimeUtc);
    }

    [Fact]
    public async Task A_sleep_period_read_back_carries_its_full_sample_runs()
    {
        // The native client charts these, so the route hands them over whole — unlike the chat
        // tool, which omits them.
        var nights = ResultAssert.Succeeded(await Read().GetSleepAsync(_user, Everything, CancellationToken.None));

        var night = Assert.Single(nights, n => n.Day == new DateOnly(2023, 1, 6));
        Assert.Equal(96, night.Reading.HeartRate.Samples.Count);
        Assert.Equal(96, night.Reading.Hrv.Samples.Count);
    }

    [Fact]
    public async Task Workouts_sessions_and_tags_each_answer_their_own_window()
    {
        var service = Read();

        Assert.Equal(3, ResultAssert.Succeeded(
            await service.GetWorkoutsAsync(_user, Everything, CancellationToken.None)).Count);
        Assert.Equal(3, ResultAssert.Succeeded(
            await service.GetSessionsAsync(_user, Everything, CancellationToken.None)).Count);
        Assert.Equal(3, ResultAssert.Succeeded(
            await service.GetTagsAsync(_user, Everything, CancellationToken.None)).Count);
    }

    [Fact]
    public async Task A_backwards_window_is_refused_on_an_event_read_too()
    {
        var backwards = new TimeWindow(
            new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var refused = await Read().GetWorkoutsAsync(_user, backwards, CancellationToken.None);

        Assert.IsType<Problem.Invalid>(ResultAssert.Failed(refused));
    }

    // ---- Chat tools ----

    [Fact]
    public async Task The_chat_tool_hands_over_nights_without_their_sample_runs()
    {
        // A night carries ~96 heart-rate and ~96 HRV samples. Handing a week of those to a model
        // spends over a thousand numbers of its context on something it cannot read.
        var nights = await Read().GetOuraSleepNightsForChatAsync(
            _user, new DayRange(new DateOnly(2023, 1, 1), new DateOnly(2023, 1, 31)));

        var night = Assert.Single(nights, n => n.Day == new DateOnly(2023, 1, 6));
        Assert.Equal(8940, night.DeepSleepSeconds);
        Assert.Equal(100, night.AverageHrv);
        Assert.True(night.IsMainSleep);
    }

    [Fact]
    public async Task The_sleep_tool_answers_only_for_the_days_asked_for()
    {
        // The archive holds a night filed under the 6th and another filed under the 8th that began
        // on the evening of the 7th. Asking for the 6th alone must not drag in the 8th's, even
        // though the window the read runs on is widened past both ends to catch it.
        var nights = await Read().GetOuraSleepNightsForChatAsync(
            _user, new DayRange(new DateOnly(2023, 1, 6), new DateOnly(2023, 1, 6)));

        Assert.Equal(new DateOnly(2023, 1, 6), Assert.Single(nights).Day);
    }

    [Fact]
    public async Task A_chat_tool_asked_for_more_days_than_it_will_answer_keeps_the_recent_end()
    {
        // A model asking for four years does not know what that costs. The cap keeps the most
        // recent 92 days, which here hold none of the archive — while the HTTP route, whose
        // consumer asked for exactly what it wanted, still answers for the whole range.
        var fourYears = new DayRange(new DateOnly(2023, 1, 1), new DateOnly(2026, 12, 31));

        Assert.Empty(await Read().GetOuraDaysForChatAsync(_user, fourYears));
        Assert.NotEmpty(ResultAssert.Succeeded(
            await Read().GetDaysAsync(_user, fourYears, CancellationToken.None)));
    }

    [Fact]
    public async Task A_chat_tool_asked_for_an_impossible_range_answers_with_nothing()
    {
        // The tool protocol carries data or nothing, so a refusal reads to the model as "no Oura
        // data for that range", which is what it means.
        var backwards = new DayRange(new DateOnly(2024, 1, 1), new DateOnly(2023, 1, 1));

        Assert.Empty(await Read().GetOuraDaysForChatAsync(_user, backwards));
    }

    private static readonly TimeWindow Everything = new(
        new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private OuraReadService Read() => new(_database.CreateContext());
}
