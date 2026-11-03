using System.Text.Json;
using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Oura;
using lionheart.Model.Oura.Readings;
using lionheart.Model.Oura.Wire;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Oura.Import;

/// <summary>
/// Loads an archive of previously-pulled Oura responses — one <c>.jsonl</c> file per collection —
/// into the Oura tables.
/// </summary>
/// <remarks>
/// Years of history already sit on disk from an earlier project, and re-pulling them through the
/// API would be slow, rate-limited, and would need a credential. Importing gives the same result
/// now, and gives the iOS client a populated database to develop against.
/// <para>
/// The files are real but old, so they are treated as untrusted input. Every line is parsed and
/// mapped on its own; a line that cannot be read becomes an
/// <see cref="OuraImportRejection"/> naming the file, the line number and the reason, and the run
/// carries on. Nothing is silently dropped and nothing aborts the import.
/// </para>
/// <para>
/// Running it twice stores nothing the second time. That falls out of the schema rather than being
/// arranged here: each table is keyed by Oura's own document id, so a record already held takes
/// the same payload again and the change tracker sees no change.
/// </para>
/// <para>
/// Only Oura data is imported. The same archive holds historical training data, which the owner's
/// decision of 2026-07-19 abandoned with no backfill (docs/data-remodel.md); this importer does
/// not read it and has no switch that would.
/// </para>
/// </remarks>
public sealed class OuraArchiveImporter
{
    /// <summary>
    /// Oura's payloads carry explicit snake_case names on every member, so no naming policy
    /// applies — the same options the live client reads responses with.
    /// </summary>
    private static readonly JsonSerializerOptions ArchiveJson = new(JsonSerializerOptions.Default);

    private readonly ModelContext _context;

    public OuraArchiveImporter(ModelContext context) => _context = context;

    public Task<Result<OuraImportReport, Problem>> ImportAsync(
        string directory, Guid userId, CancellationToken ct) =>
        AthleteAsync(userId, ct)
            .BindAsync(_ => Archive(directory))
            .BindAsync(archive => Steps(archive, userId).TraverseAsync(step => step(ct)))
            .MapAsync(collections => new OuraImportReport(collections, Uncollected(directory)));

    // ---- Guard ----

    /// <remarks>
    /// Named here rather than reusing <c>AthleteIdAsync</c>, which answers from an
    /// <c>IdentityUser</c>: this command is given a raw id on the command line, and a mistyped one
    /// should say so rather than fail later as a foreign-key violation.
    /// </remarks>
    private async Task<Result<Guid, Problem>> AthleteAsync(Guid userId, CancellationToken ct) =>
        await _context.LionheartUsers.AnyAsync(user => user.UserID == userId, ct)
            ? Result<Guid, Problem>.Success(userId)
            : Result<Guid, Problem>.Failure(new Problem.NotFound(
                $"No Lionheart athlete with id {userId}. Imported documents hang off one, so there is nothing to attach these to."));

    private static Result<string, Problem> Archive(string directory) =>
        Directory.Exists(directory)
            ? Result<string, Problem>.Success(directory)
            : Result<string, Problem>.Failure(new Problem.Invalid(
                nameof(directory), $"No directory at '{directory}'."));

    // ---- The collections ----

    /// <remarks>
    /// One closure per collection, for the reason given on <see cref="OuraSyncService"/>: the three
    /// types differ each time and C# cannot hold them in one list. Resilience, VO2max and
    /// cardiovascular age are read for whenever an archive holds them; the athlete's has no file
    /// for any of the three, and <see cref="ReadAsync{TWire, TDocument, TReading}"/> reports that
    /// rather than inventing an empty one. The all-day heart-rate series is not here at all: its
    /// samples carry no document id, so it has no archive shape this importer could key, and the
    /// live sync is its only way in.
    /// </remarks>
    private IReadOnlyList<Func<CancellationToken, Task<Result<OuraCollectionImport, Problem>>>> Steps(
        string directory, Guid userId) =>
    [
        ct => ReadAsync<WireDailyActivity, OuraDailyActivity, DailyActivityReading>(
            directory, "daily_activity", wire => wire.ToDocument(userId), _context.OuraDailyActivity, ct),
        ct => ReadAsync<WireDailyReadiness, OuraDailyReadiness, DailyReadinessReading>(
            directory, "daily_readiness", wire => wire.ToDocument(userId), _context.OuraDailyReadiness, ct),
        ct => ReadAsync<WireDailySleep, OuraDailySleep, DailySleepReading>(
            directory, "daily_sleep", wire => wire.ToDocument(userId), _context.OuraDailySleep, ct),
        ct => ReadAsync<WireDailySpo2, OuraDailySpo2, DailySpo2Reading>(
            directory, "daily_spo2", wire => wire.ToDocument(userId), _context.OuraDailySpo2, ct),
        ct => ReadAsync<WireDailyStress, OuraDailyStress, DailyStressReading>(
            directory, "daily_stress", wire => wire.ToDocument(userId), _context.OuraDailyStress, ct),
        ct => ReadAsync<WireDailyResilience, OuraDailyResilience, DailyResilienceReading>(
            directory, "daily_resilience", wire => wire.ToDocument(userId), _context.OuraDailyResilience, ct),
        ct => ReadAsync<WireVo2Max, OuraVo2Max, Vo2MaxReading>(
            directory, "vO2_max", wire => wire.ToDocument(userId), _context.OuraVo2Max, ct),
        ct => ReadAsync<WireDailyCardiovascularAge, OuraDailyCardiovascularAge, CardiovascularAgeReading>(
            directory, "daily_cardiovascular_age", wire => wire.ToDocument(userId), _context.OuraDailyCardiovascularAge, ct),
        ct => ReadAsync<WireSleep, OuraSleepPeriod, SleepPeriodReading>(
            directory, "sleep", wire => wire.ToDocument(userId), _context.OuraSleepPeriods, ct),
        ct => ReadAsync<WireWorkout, OuraWorkout, WorkoutReading>(
            directory, "workout", wire => wire.ToDocument(userId), _context.OuraWorkouts, ct),
        ct => ReadAsync<WireSession, OuraSession, SessionReading>(
            directory, "session", wire => wire.ToDocument(userId), _context.OuraSessions, ct),
        ct => ReadAsync<WireTag, OuraTag, TagReading>(
            directory, "tag", wire => wire.ToDocument(userId), _context.OuraTags, ct),
    ];

    /// <summary>
    /// Every file in the archive this importer does not read, so the report says what it left
    /// behind instead of leaving a reader to compare directory listings.
    /// </summary>
    /// <remarks>
    /// How the deliberate omissions surface: the archive holds a <c>sleep_time.jsonl</c>, which is
    /// Oura's bedtime advice rather than a measurement and is not stored (docs/oura-integration.md).
    /// Reporting the leftovers generally beats hardcoding that one name, because a collection Oura
    /// adds later shows up here on the next run.
    /// </remarks>
    private static IReadOnlyList<string> Uncollected(string directory)
    {
        var collected = Collections.Select(collection => $"{collection}.jsonl").ToHashSet();

        return
        [
            .. Directory.EnumerateFiles(directory, "*.jsonl")
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(name => !collected.Contains(name))
                .Order()
        ];
    }

    private static readonly string[] Collections =
    [
        "daily_activity", "daily_readiness", "daily_sleep", "daily_spo2", "daily_stress",
        "daily_resilience", "vO2_max", "daily_cardiovascular_age", "sleep", "workout", "session", "tag"
    ];

    // ---- One collection ----

    private async Task<Result<OuraCollectionImport, Problem>> ReadAsync<TWire, TDocument, TReading>(
        string directory,
        string collection,
        Func<TWire, Result<TDocument, OuraDocumentDefects>> map,
        DbSet<TDocument> table,
        CancellationToken ct)
        where TDocument : OuraDocument<TReading>
        where TReading : class
    {
        var name = $"{collection}.jsonl";
        var path = Path.Combine(directory, name);

        if (!File.Exists(path))
        {
            return Imported(collection, new OuraCollectionOutcome.NoArchiveFile(name));
        }

        var mapped = JsonLines.Read(path).Partition(line => Interpret(name, line, map));

        await table.UpsertAsync<TDocument, TReading>(mapped.Delivered, ct);
        await _context.SaveChangesAsync(ct);

        // Every record read ended up on one side or the other, so the two halves add back up to
        // the records in the file. Counting them is what the partition already did.
        return Imported(collection, new OuraCollectionOutcome.Read(
            mapped.Delivered.Count + mapped.Refused.Count, mapped.Delivered.Count, mapped.Refused));
    }

    /// <summary>
    /// One archived line turned into a document, or the reason it could not be.
    /// </summary>
    /// <remarks>
    /// Deserialization is the one step here that answers by throwing — a malformed line, or a
    /// number where a string belongs, is a <see cref="JsonException"/> — so it is caught and named
    /// at this boundary. Everything past it travels as a <see cref="Result{T, TProblem}"/>.
    /// </remarks>
    private static Result<TDocument, OuraImportRejection> Interpret<TWire, TDocument>(
        string file,
        (int Number, string Text) line,
        Func<TWire, Result<TDocument, OuraDocumentDefects>> map)
    {
        TWire? wire;
        try
        {
            wire = JsonSerializer.Deserialize<TWire>(line.Text, ArchiveJson);
        }
        catch (JsonException exception)
        {
            return Rejected<TDocument>(file, line.Number, $"unreadable JSON ({exception.Message})");
        }

        return wire is null
            ? Rejected<TDocument>(file, line.Number, "the line is the JSON literal null")
            : map(wire).MapError(defects => new OuraImportRejection(file, line.Number, defects.Description));
    }

    private static Result<TDocument, OuraImportRejection> Rejected<TDocument>(
        string file, int line, string reason) =>
        Result<TDocument, OuraImportRejection>.Failure(new OuraImportRejection(file, line, reason));

    private static Result<OuraCollectionImport, Problem> Imported(
        string collection, OuraCollectionOutcome outcome) =>
        Result<OuraCollectionImport, Problem>.Success(new OuraCollectionImport(collection, outcome));
}
