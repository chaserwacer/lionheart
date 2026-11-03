using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Oura;
using lionheart.Model.Oura.Readings;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Model.Chat.Tools;

namespace lionheart.Services.Oura;

public interface IOuraReadService
{
    /// <summary>Every day in the range the ring recorded anything for, in order.</summary>
    Task<Result<IReadOnlyList<OuraDayView>, Problem>> GetDaysAsync(
        IdentityUser user, DayRange range, CancellationToken ct);

    /// <summary>One day, or a refusal if the ring recorded nothing for it.</summary>
    Task<Result<OuraDayView, Problem>> GetDayAsync(IdentityUser user, DateOnly day, CancellationToken ct);

    /// <summary>
    /// The most recent day any collection covers, or a refusal if nothing is stored at all.
    /// </summary>
    Task<Result<OuraDayView, Problem>> GetLatestDayAsync(IdentityUser user, CancellationToken ct);

    Task<Result<IReadOnlyList<OuraEventView<SleepPeriodReading>>, Problem>> GetSleepAsync(
        IdentityUser user, TimeWindow window, CancellationToken ct);

    Task<Result<IReadOnlyList<OuraEventView<WorkoutReading>>, Problem>> GetWorkoutsAsync(
        IdentityUser user, TimeWindow window, CancellationToken ct);

    Task<Result<IReadOnlyList<OuraEventView<SessionReading>>, Problem>> GetSessionsAsync(
        IdentityUser user, TimeWindow window, CancellationToken ct);

    Task<Result<IReadOnlyList<OuraEventView<TagReading>>, Problem>> GetTagsAsync(
        IdentityUser user, TimeWindow window, CancellationToken ct);
}

/// <summary>
/// One night's sleep as the chat model reads it.
/// </summary>
/// <remarks>
/// The same facts as <see cref="SleepPeriodReading"/> without the overnight sample runs. A night
/// carries around ninety heart-rate samples and ninety HRV samples, which a native client charts
/// and a language model can do nothing with but spend its context on; a week of nights would be
/// well over a thousand numbers. The HTTP route still answers with the series in full.
/// </remarks>
public sealed record OuraSleepSummary(
    DateOnly Day,
    DateTimeOffset BedtimeStart,
    string Kind,
    bool IsMainSleep,
    int TimeInBedSeconds,
    int TotalSleepSeconds,
    int DeepSleepSeconds,
    int RemSleepSeconds,
    int LightSleepSeconds,
    int AwakeSeconds,
    int Efficiency,
    int LatencySeconds,
    double? AverageHeartRate,
    int? LowestHeartRate,
    int? AverageHrv,
    double? AverageBreath);

/// <summary>
/// Reading the stored Oura data back.
/// </summary>
/// <remarks>
/// Read-shaped throughout: a native client is the consumer, so each route answers with the
/// documents themselves rather than with anything computed. Days are keyed by the day Oura filed
/// them under; events are windowed on the stamped StartTime column, the same way contexts and
/// completed sessions are.
/// </remarks>
[ToolProvider]
public sealed class OuraReadService : IOuraReadService
{
    private readonly ModelContext _context;

    public OuraReadService(ModelContext context) => _context = context;

    public Task<Result<IReadOnlyList<OuraDayView>, Problem>> GetDaysAsync(
        IdentityUser user, DayRange range, CancellationToken ct) =>
        range.Ordered().BindAsync(ordered => DaysAsync(Guid.Parse(user.Id), ordered, ct));

    public Task<Result<OuraDayView, Problem>> GetDayAsync(
        IdentityUser user, DateOnly day, CancellationToken ct) =>
        DaysAsync(Guid.Parse(user.Id), new DayRange(day, day), ct)
            .BindAsync(days => days is [var only]
                ? Result<OuraDayView, Problem>.Success(only)
                : Result<OuraDayView, Problem>.Failure(
                    new Problem.NotFound($"No Oura data is stored for {day:yyyy-MM-dd}.")));

    /// <remarks>
    /// What a client syncs from: the newest stored day says how far behind the store is. It is
    /// answered whole rather than as a bare date because the newest day is usually provisional —
    /// today's activity is filed before the night's sleep is — and the caller can see which
    /// collections it holds.
    /// </remarks>
    public Task<Result<OuraDayView, Problem>> GetLatestDayAsync(IdentityUser user, CancellationToken ct) =>
        LatestDayAsync(Guid.Parse(user.Id), ct)
            .BindAsync(day => GetDayAsync(user, day, ct));

    public Task<Result<IReadOnlyList<OuraEventView<SleepPeriodReading>>, Problem>> GetSleepAsync(
        IdentityUser user, TimeWindow window, CancellationToken ct) =>
        EventsAsync<OuraSleepPeriod, SleepPeriodReading>(user, _context.OuraSleepPeriods, window, ct);

    public Task<Result<IReadOnlyList<OuraEventView<WorkoutReading>>, Problem>> GetWorkoutsAsync(
        IdentityUser user, TimeWindow window, CancellationToken ct) =>
        EventsAsync<OuraWorkout, WorkoutReading>(user, _context.OuraWorkouts, window, ct);

    public Task<Result<IReadOnlyList<OuraEventView<SessionReading>>, Problem>> GetSessionsAsync(
        IdentityUser user, TimeWindow window, CancellationToken ct) =>
        EventsAsync<OuraSession, SessionReading>(user, _context.OuraSessions, window, ct);

    public Task<Result<IReadOnlyList<OuraEventView<TagReading>>, Problem>> GetTagsAsync(
        IdentityUser user, TimeWindow window, CancellationToken ct) =>
        EventsAsync<OuraTag, TagReading>(user, _context.OuraTags, window, ct);

    // ---- Chat tools ----
    //
    // The conversation's way into this data. Both end their chain with a Match, which is what a
    // transport entry point does: the tool protocol carries data or nothing, so a refusal becomes
    // an empty answer and the model reads it as "no Oura data for that range" — which is what it
    // means.

    /// <summary>
    /// The most days a chat tool will answer with at once.
    /// </summary>
    /// <remarks>
    /// A model asking for a year does not know what it is asking for: each day view is around
    /// forty numbers, so a year is tens of thousands of them spent where a quarter would have
    /// answered the question. The cap keeps the most recent days of whatever was asked and is
    /// stated in both tool descriptions, so the model can ask again for an earlier window instead
    /// of silently getting less than it believes.
    /// </remarks>
    private const int ChatDayLimit = 92;

    [Tool(Name = "GetOuraDays",
        Description = "Oura's daily documents for a range of days: activity, readiness, sleep, SpO2, stress and resilience scores with their contributors. One entry per day the ring recorded anything for. At most 92 days per call — if the range is longer, only its most recent 92 days are returned, so ask again for an earlier window.")]
    public Task<IReadOnlyList<OuraDayView>> GetOuraDaysForChatAsync(IdentityUser user, DayRange range) =>
        GetDaysAsync(user, Recent(range), CancellationToken.None)
            .MatchAsync(days => days, _ => []);

    [Tool(Name = "GetOuraSleepNights",
        Description = "Per-night sleep detail from Oura for a range of days: stage durations, efficiency, latency, lowest and average heart rate, average HRV and breathing rate. Use this when the daily sleep score is not enough to explain a night. At most 92 days per call — if the range is longer, only its most recent 92 days are returned, so ask again for an earlier window.")]
    public Task<IReadOnlyList<OuraSleepSummary>> GetOuraSleepNightsForChatAsync(
        IdentityUser user, DayRange range) =>
        NightsForChatAsync(user, Recent(range));

    /// <remarks>
    /// Asked in days but read on the clock, so the window is widened past both ends and the
    /// answer filtered back to the days asked for. A night Oura files under the first day of the
    /// range began the evening before it, and a window starting at that day's midnight would
    /// simply miss it.
    /// </remarks>
    private Task<IReadOnlyList<OuraSleepSummary>> NightsForChatAsync(IdentityUser user, DayRange range) =>
        GetSleepAsync(user, Spanning(range), CancellationToken.None)
            .MatchAsync(
                nights => Summaries(nights.Where(night =>
                    night.Day >= range.From && night.Day <= range.To)),
                _ => []);

    /// <summary>The last <see cref="ChatDayLimit"/> days of a range, or the range if it is shorter.</summary>
    private static DayRange Recent(DayRange range) =>
        range.Days > ChatDayLimit
            ? new DayRange(range.To.AddDays(1 - ChatDayLimit), range.To)
            : range;

    /// <summary>
    /// A day range as the window the event reads take.
    /// </summary>
    /// <remarks>
    /// Widened to the whole of both end days at the widest offset either could have been lived at,
    /// so a night that began late on the first day is not cut off by a window that started at its
    /// midnight in UTC.
    /// </remarks>
    private static TimeWindow Spanning(DayRange range) =>
        new(new DateTimeOffset(range.From.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(-1),
            new DateTimeOffset(range.To.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero).AddDays(1));

    private static IReadOnlyList<OuraSleepSummary> Summaries(
        IEnumerable<OuraEventView<SleepPeriodReading>> nights) =>
    [
        .. nights.Select(night => new OuraSleepSummary(
            night.Day,
            night.Period.StartTime,
            night.Reading.Kind,
            night.Reading.IsMainSleep,
            night.Reading.TimeInBedSeconds,
            night.Reading.TotalSleepSeconds,
            night.Reading.DeepSleepSeconds,
            night.Reading.RemSleepSeconds,
            night.Reading.LightSleepSeconds,
            night.Reading.AwakeSeconds,
            night.Reading.Efficiency,
            night.Reading.LatencySeconds,
            MeasuredHeartRate(night.Reading),
            night.Reading.LowestHeartRate,
            night.Reading.AverageHrv,
            night.Reading.AverageBreath))
    ];

    /// <summary>
    /// The period's mean heart rate, or nothing where the ring took no reading across it.
    /// </summary>
    /// <remarks>
    /// Oura answers <c>0</c> rather than null on 148 of the archived periods, nearly all brief
    /// naps. The stored document keeps that zero deliberately, because it is what Oura said and a
    /// reader can tell by the sample run beside it being all null. This summary omits the sample
    /// runs, so that tell is gone — and a bare <c>0</c> is a heart rate the model would reason
    /// with. Absence is the honest value once the evidence for the zero has been left out.
    /// </remarks>
    private static double? MeasuredHeartRate(SleepPeriodReading night) =>
        night.AverageHeartRate is 0 && night.HeartRate.Samples.All(sample => sample is null)
            ? null
            : night.AverageHeartRate;

    // ---- Reads ----

    /// <summary>
    /// The six daily collections for a range, stitched into one row per day that has any of them.
    /// </summary>
    /// <remarks>
    /// Six indexed range queries and a join in memory, rather than one query with five outer
    /// joins. The collections cover different sets of days and none is a reliable spine to join
    /// the others onto, the payloads are JSON columns a relational join could not read into
    /// anyway, and a range read at one athlete's volume is a few hundred rows.
    /// <para>
    /// Oura has issued more than one document for the same day in the SpO2 collection, so each
    /// collection contributes at most one reading per day here, taken in document-id order so the
    /// answer does not change between identical calls.
    /// </para>
    /// </remarks>
    private async Task<Result<IReadOnlyList<OuraDayView>, Problem>> DaysAsync(
        Guid userId, DayRange range, CancellationToken ct)
    {
        var activity = await DailyAsync<OuraDailyActivity, DailyActivityReading>(_context.OuraDailyActivity, userId, range, ct);
        var readiness = await DailyAsync<OuraDailyReadiness, DailyReadinessReading>(_context.OuraDailyReadiness, userId, range, ct);
        var sleep = await DailyAsync<OuraDailySleep, DailySleepReading>(_context.OuraDailySleep, userId, range, ct);
        var spo2 = await DailyAsync<OuraDailySpo2, DailySpo2Reading>(_context.OuraDailySpo2, userId, range, ct);
        var stress = await DailyAsync<OuraDailyStress, DailyStressReading>(_context.OuraDailyStress, userId, range, ct);
        var resilience = await DailyAsync<OuraDailyResilience, DailyResilienceReading>(_context.OuraDailyResilience, userId, range, ct);

        IReadOnlyList<OuraDayView> days =
        [
            .. activity.Keys
                .Union(readiness.Keys).Union(sleep.Keys)
                .Union(spo2.Keys).Union(stress.Keys).Union(resilience.Keys)
                .OrderBy(day => day)
                .Select(day => new OuraDayView(
                    day,
                    activity.GetValueOrDefault(day),
                    readiness.GetValueOrDefault(day),
                    sleep.GetValueOrDefault(day),
                    spo2.GetValueOrDefault(day),
                    stress.GetValueOrDefault(day),
                    resilience.GetValueOrDefault(day)))
        ];

        return Result<IReadOnlyList<OuraDayView>, Problem>.Success(days);
    }

    /// <summary>The newest day across the six daily collections.</summary>
    /// <remarks>
    /// Each collection is asked for its own newest day because they cover different days, and the
    /// latest of the six is the answer.
    /// </remarks>
    private async Task<Result<DateOnly, Problem>> LatestDayAsync(Guid userId, CancellationToken ct)
    {
        DateOnly?[] newest =
        [
            await NewestAsync<OuraDailyActivity, DailyActivityReading>(_context.OuraDailyActivity, userId, ct),
            await NewestAsync<OuraDailyReadiness, DailyReadinessReading>(_context.OuraDailyReadiness, userId, ct),
            await NewestAsync<OuraDailySleep, DailySleepReading>(_context.OuraDailySleep, userId, ct),
            await NewestAsync<OuraDailySpo2, DailySpo2Reading>(_context.OuraDailySpo2, userId, ct),
            await NewestAsync<OuraDailyStress, DailyStressReading>(_context.OuraDailyStress, userId, ct),
            await NewestAsync<OuraDailyResilience, DailyResilienceReading>(_context.OuraDailyResilience, userId, ct),
        ];

        return newest.Max() is DateOnly latest
            ? Result<DateOnly, Problem>.Success(latest)
            : Result<DateOnly, Problem>.Failure(new Problem.NotFound("No Oura data is stored yet."));
    }

    private static Task<DateOnly?> NewestAsync<TDocument, TReading>(
        DbSet<TDocument> table, Guid userId, CancellationToken ct)
        where TDocument : OuraDocument<TReading>
        where TReading : class =>
        table
            .AsNoTracking()
            .Where(document => document.UserId == userId)
            .OrderByDescending(document => document.Day)
            .Select(document => (DateOnly?)document.Day)
            .FirstOrDefaultAsync(ct);

    private static async Task<Dictionary<DateOnly, TReading>> DailyAsync<TDocument, TReading>(
        DbSet<TDocument> table, Guid userId, DayRange range, CancellationToken ct)
        where TDocument : OuraDocument<TReading>
        where TReading : class
    {
        var documents = await table
            .AsNoTracking()
            .Where(document => document.UserId == userId
                               && document.Day >= range.From
                               && document.Day <= range.To)
            .OrderBy(document => document.Day)
            .ThenBy(document => document.OuraId)
            .ToListAsync(ct);

        return documents
            .GroupBy(document => document.Day)
            .ToDictionary(group => group.Key, group => group.First().Reading);
    }

    private static Task<Result<IReadOnlyList<OuraEventView<TReading>>, Problem>> EventsAsync<TDocument, TReading>(
        IdentityUser user, DbSet<TDocument> table, TimeWindow window, CancellationToken ct)
        where TDocument : OuraEvent<TReading>
        where TReading : class =>
        window.Ordered().BindAsync(ordered => InWindowAsync<TDocument, TReading>(
            table, Guid.Parse(user.Id), ordered, ct));

    private static async Task<Result<IReadOnlyList<OuraEventView<TReading>>, Problem>> InWindowAsync<TDocument, TReading>(
        DbSet<TDocument> table, Guid userId, TimeWindow window, CancellationToken ct)
        where TDocument : OuraEvent<TReading>
        where TReading : class
    {
        var (fromUtc, toUtc) = (window.From.UtcDateTime, window.To.UtcDateTime);

        var documents = await table
            .AsNoTracking()
            .Where(document => document.UserId == userId)
            .Where(document => EF.Property<DateTime>(document, "StartTime") >= fromUtc
                               && EF.Property<DateTime>(document, "StartTime") <= toUtc)
            .OrderBy(document => EF.Property<DateTime>(document, "StartTime"))
            .ToListAsync(ct);

        IReadOnlyList<OuraEventView<TReading>> views =
        [
            .. documents.Select(document => new OuraEventView<TReading>(
                document.OuraId, document.Day, document.Period, document.Reading))
        ];

        return Result<IReadOnlyList<OuraEventView<TReading>>, Problem>.Success(views);
    }
}
