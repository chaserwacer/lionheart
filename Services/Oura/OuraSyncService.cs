using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Oura;
using lionheart.Model.Oura.Readings;
using lionheart.Model.Oura.Wire;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Oura;

/// <summary>What one collection's sync did, including what it could not store.</summary>
public sealed record OuraCollectionTally(
    string Collection,
    int Received,
    int Stored,
    IReadOnlyList<string> Rejected);

/// <summary>What a sync run did, per collection.</summary>
public sealed record OuraSyncReport(DayRange Range, IReadOnlyList<OuraCollectionTally> Collections)
{
    public int Stored => Collections.Sum(collection => collection.Stored);
    public int Rejected => Collections.Sum(collection => collection.Rejected.Count);
}

public interface IOuraSyncService
{
    /// <summary>
    /// Pulls every supported collection for a range of days and stores what came back.
    /// </summary>
    Task<Result<OuraSyncReport, Problem>> SyncAsync(IdentityUser user, DayRange range, CancellationToken ct);
}

/// <summary>
/// Brings the Oura tables up to date from Oura's API.
/// </summary>
/// <remarks>
/// Ten collections, one shape: read the pages for a date range, map each document, store it.
/// Because Oura's own document id is each table's primary key, storing is an upsert with no sync
/// ledger behind it — the row either exists and takes the newer payload or it does not exist yet.
/// Nothing here has to reason about which days are still provisional: re-syncing a day Oura has
/// since finalized simply applies the newer payload.
/// <para>
/// A document Oura sends that this application cannot read is counted and described rather than
/// failing the run: one malformed record must not cost a month of good days. A failure to reach
/// Oura, to authenticate, or to read a page at all is a <see cref="Problem.Upstream"/> and does
/// end the run, because nothing after it would mean anything.
/// </para>
/// <para>
/// <strong>Unverified against the live API.</strong> Running this needs the athlete's Oura
/// credential, which was deliberately not used while building it. What is covered by tests is the
/// mapping and the paging, against recorded Oura payloads — see docs/oura-integration.md.
/// </para>
/// </remarks>
public sealed class OuraSyncService : IOuraSyncService
{
    private const string OuraApplicationName = "oura";

    private readonly ModelContext _context;
    private readonly OuraClient _client;

    public OuraSyncService(ModelContext context, HttpClient httpClient)
    {
        _context = context;
        _client = new OuraClient(httpClient);
    }

    public Task<Result<OuraSyncReport, Problem>> SyncAsync(
        IdentityUser user, DayRange range, CancellationToken ct) =>
        _context.AthleteIdAsync(user, ct)
            .BindAsync(userId => range.Ordered().Map(ordered => (userId, ordered)))
            .BindAsync(asked => CredentialAsync(asked.userId, asked.ordered, ct))
            .BindAsync(run => Steps(run).TraverseAsync(step => step(ct)))
            .MapAsync(tallies => new OuraSyncReport(range, tallies));

    // ---- Guard ----

    /// <summary>The range to pull and the credential to pull it with, or why neither is available.</summary>
    private sealed record SyncRun(Guid UserId, DayRange Range, string Token);

    private async Task<Result<SyncRun, Problem>> CredentialAsync(
        Guid userId, DayRange range, CancellationToken ct)
    {
        var token = await _context.ApiAccessTokens
            .Where(access => access.UserID == userId && access.ApplicationName == OuraApplicationName)
            .Select(access => access.PersonalAccessToken)
            .FirstOrDefaultAsync(ct);

        return string.IsNullOrWhiteSpace(token)
            ? Result<SyncRun, Problem>.Failure(new Problem.Conflict(
                "This account has no Oura credential stored. Add one before syncing."))
            : Result<SyncRun, Problem>.Success(new SyncRun(userId, range, token));
    }

    // ---- The collections ----

    /// <remarks>
    /// Each collection is a closure over its own three types, because a heterogeneous list of them
    /// cannot be written any other way in C#. What they share — page, map, upsert, tally — is
    /// <see cref="CollectAsync{TWire, TDocument, TReading}"/>, written once.
    /// </remarks>
    private IReadOnlyList<Func<CancellationToken, Task<Result<OuraCollectionTally, Problem>>>> Steps(SyncRun run) =>
    [
        // The activity collection answers an end_date of its own last day with that day missing,
        // so it is asked for one day more. Applied only here, so the workaround stays attached to
        // the quirk it exists for rather than looking like a convention.
        ct => CollectAsync<WireDailyActivity, OuraDailyActivity, DailyActivityReading>(
            run, "daily_activity", endDayShift: 1, wire => wire.ToDocument(run.UserId), _context.OuraDailyActivity, ct),
        ct => CollectAsync<WireDailyReadiness, OuraDailyReadiness, DailyReadinessReading>(
            run, "daily_readiness", endDayShift: 0, wire => wire.ToDocument(run.UserId), _context.OuraDailyReadiness, ct),
        ct => CollectAsync<WireDailySleep, OuraDailySleep, DailySleepReading>(
            run, "daily_sleep", endDayShift: 0, wire => wire.ToDocument(run.UserId), _context.OuraDailySleep, ct),
        ct => CollectAsync<WireDailySpo2, OuraDailySpo2, DailySpo2Reading>(
            run, "daily_spo2", endDayShift: 0, wire => wire.ToDocument(run.UserId), _context.OuraDailySpo2, ct),
        ct => CollectAsync<WireDailyStress, OuraDailyStress, DailyStressReading>(
            run, "daily_stress", endDayShift: 0, wire => wire.ToDocument(run.UserId), _context.OuraDailyStress, ct),
        ct => CollectAsync<WireDailyResilience, OuraDailyResilience, DailyResilienceReading>(
            run, "daily_resilience", endDayShift: 0, wire => wire.ToDocument(run.UserId), _context.OuraDailyResilience, ct),
        ct => CollectAsync<WireSleep, OuraSleepPeriod, SleepPeriodReading>(
            run, "sleep", endDayShift: 0, wire => wire.ToDocument(run.UserId), _context.OuraSleepPeriods, ct),
        ct => CollectAsync<WireWorkout, OuraWorkout, WorkoutReading>(
            run, "workout", endDayShift: 0, wire => wire.ToDocument(run.UserId), _context.OuraWorkouts, ct),
        ct => CollectAsync<WireSession, OuraSession, SessionReading>(
            run, "session", endDayShift: 0, wire => wire.ToDocument(run.UserId), _context.OuraSessions, ct),
        ct => CollectAsync<WireTag, OuraTag, TagReading>(
            run, "tag", endDayShift: 0, wire => wire.ToDocument(run.UserId), _context.OuraTags, ct),
    ];

    private Task<Result<OuraCollectionTally, Problem>> CollectAsync<TWire, TDocument, TReading>(
        SyncRun run,
        string collection,
        int endDayShift,
        Func<TWire, Result<TDocument, OuraDocumentDefects>> map,
        DbSet<TDocument> table,
        CancellationToken ct)
        where TDocument : OuraDocument<TReading>
        where TReading : class =>
        _client.ReadAsync<TWire>(collection, run.Range.From, run.Range.To.AddDays(endDayShift), run.Token, ct)
            .MapAsync(received => StoreAsync<TDocument, TReading>(
                collection, received.Count, received.Partition(map), table, ct));

    // ---- Commit ----

    /// <summary>
    /// Writes the documents that mapped, leaving the ones that did not as descriptions in the tally.
    /// </summary>
    private async Task<OuraCollectionTally> StoreAsync<TDocument, TReading>(
        string collection,
        int received,
        Partitioned<TDocument, OuraDocumentDefects> mapped,
        DbSet<TDocument> table,
        CancellationToken ct)
        where TDocument : OuraDocument<TReading>
        where TReading : class
    {
        await table.UpsertAsync<TDocument, TReading>(mapped.Delivered, ct);
        await _context.SaveChangesAsync(ct);

        return new OuraCollectionTally(
            collection,
            received,
            mapped.Delivered.Count,
            [.. mapped.Refused.Select(defects => defects.Description)]);
    }
}
