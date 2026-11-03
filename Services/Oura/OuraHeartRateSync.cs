using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Oura;
using lionheart.Model.Oura.Readings;
using lionheart.Model.Oura.Wire;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Oura;

/// <summary>
/// Brings the all-day heart-rate series up to date for a range of days.
/// </summary>
/// <remarks>
/// The one collection the generic sync step cannot take. Oura serves it by instants rather than
/// days, caps how much one request may span, and sends bare samples with no document id — so it is
/// read in slices of whole UTC days, gathered into one run per UTC day, and each day Oura answered
/// for replaces the stored one.
/// <para>
/// Whole UTC days are what make the replace safe. A slice always starts and ends at a UTC
/// midnight, so every day it touches is complete as of this pull; a stored day is never overwritten
/// by part of itself. The range runs one UTC day past the last day asked for, because the athlete's
/// evening — anywhere west of Greenwich — is already the next day in UTC.
/// </para>
/// <para>
/// Each slice is stored as it arrives. A long backfill that is rate-limited halfway keeps the
/// slices it finished, and re-running it rewrites them with what Oura holds, which is the same.
/// </para>
/// </remarks>
internal sealed class OuraHeartRateSync
{
    public const string Collection = "heartrate";

    /// <summary>
    /// The longest span one request asks for.
    /// </summary>
    /// <remarks>
    /// Oura's OpenAPI description (1.43) states no maximum for this route; client libraries and
    /// community notes report requests refused past about a month. Thirty days stays under that,
    /// and costs a three-year backfill around forty requests plus their pages.
    /// </remarks>
    public const int SliceDays = 30;

    private readonly ModelContext _context;
    private readonly OuraClient _client;

    public OuraHeartRateSync(ModelContext context, OuraClient client)
    {
        _context = context;
        _client = client;
    }

    public Task<Result<OuraCollectionTally, Problem>> SyncAsync(
        Guid userId, DayRange range, string token, CancellationToken ct) =>
        Slices(range)
            .TraverseAsync(slice => SliceAsync(userId, slice, token, ct))
            .MapAsync(tallies => new OuraCollectionTally(
                Collection,
                tallies.Sum(tally => tally.Received),
                tallies.Sum(tally => tally.Stored),
                [.. tallies.SelectMany(tally => tally.Rejected)]));

    /// <summary>
    /// The range as consecutive spans of whole UTC days, each at most <see cref="SliceDays"/> long,
    /// running from the first day's UTC midnight to the UTC midnight two days after the last.
    /// </summary>
    public static IReadOnlyList<TimeWindow> Slices(DayRange range)
    {
        var first = range.From.DayNumber;
        var end = range.To.AddDays(2).DayNumber;

        return
        [
            .. Enumerable.Range(0, (end - first + SliceDays - 1) / SliceDays)
                .Select(index => first + index * SliceDays)
                .Select(start => new TimeWindow(Midnight(start), Midnight(Math.Min(start + SliceDays, end))))
        ];
    }

    private static DateTimeOffset Midnight(int dayNumber) =>
        new(DateOnly.FromDayNumber(dayNumber).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private Task<Result<OuraCollectionTally, Problem>> SliceAsync(
        Guid userId, TimeWindow slice, string token, CancellationToken ct) =>
        _client.ReadAsync<WireHeartRate>(Collection, slice.From, slice.To, token, ct)
            .MapAsync(received => StoreAsync(userId, slice, received.Count, received.Partition(wire => wire.ToSample()), ct));

    // ---- Commit ----

    /// <summary>
    /// Replaces every UTC day in the slice that Oura answered for, leaving unreadable samples as
    /// descriptions in the tally.
    /// </summary>
    /// <remarks>
    /// A sample outside the slice is dropped rather than filed: Oura's window bounds are not
    /// documented as inclusive or exclusive, and a sample at the closing midnight belongs to the
    /// next slice, which will file it with the rest of its day. A sample Oura repeats at the same
    /// instant is stored once.
    /// </remarks>
    private async Task<OuraCollectionTally> StoreAsync(
        Guid userId,
        TimeWindow slice,
        int received,
        Partitioned<HeartRateSample, OuraDocumentDefects> mapped,
        CancellationToken ct)
    {
        var days = mapped.Delivered
            .Where(sample => sample.At >= slice.From && sample.At < slice.To)
            .DistinctBy(sample => sample.At)
            .GroupBy(sample => DateOnly.FromDateTime(sample.At.UtcDateTime))
            .ToDictionary(
                day => day.Key,
                day => new HeartRateDayReading([.. day.OrderBy(sample => sample.At)]));

        await ReplaceAsync(userId, days, ct);
        await _context.SaveChangesAsync(ct);

        return new OuraCollectionTally(
            Collection,
            received,
            days.Values.Sum(day => day.Samples.Count),
            [.. mapped.Refused.Select(defects => defects.Description)]);
    }

    private async Task ReplaceAsync(
        Guid userId, IReadOnlyDictionary<DateOnly, HeartRateDayReading> days, CancellationToken ct)
    {
        var keys = days.Keys.ToList();

        // Tracked on purpose: Replace mutates these, and an unchanged day then writes nothing.
        var stored = await _context.OuraHeartRateDays
            .Where(day => day.UserId == userId && keys.Contains(day.Day))
            .ToDictionaryAsync(day => day.Day, ct);

        foreach (var (day, reading) in days)
        {
            if (stored.TryGetValue(day, out var existing)) existing.Replace(reading);
            else _context.OuraHeartRateDays.Add(new OuraHeartRateDay(userId, day, reading));
        }
    }
}
