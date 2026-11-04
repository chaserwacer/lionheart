using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Model.Health;
using lionheart.Model.Training.Sessions;
using lionheart.Services.Cardio;
using lionheart.Services.Oura;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Model.Chat.Tools;

namespace lionheart.Services.Health;

public interface IRecordedRunService
{
    /// <summary>One imported run read whole, or not found for a session that is not one of the athlete's imported runs.</summary>
    Task<Result<RecordedRunView, Problem>> GetAsync(IdentityUser user, Guid trainingSessionId, CancellationToken ct);
}

/// <summary>
/// Reads the runs imported from Apple Health back: for the run's own page, and as summaries for the
/// chat model.
/// </summary>
/// <remarks>
/// The heart rate is the recording app's where it wrote any, and otherwise the ring's all-day
/// series over the same clock time, which the page labels; the maximum the zones are drawn from is
/// the cardio report's, so a run reads the same here as there.
/// </remarks>
[ToolProvider]
public sealed class RecordedRunService : IRecordedRunService
{
    /// <summary>The most days one chat call reads, the same bound the cardio tool keeps.</summary>
    private const int ChatDayLimit = 92;

    private readonly ModelContext _context;
    private readonly ICardioReportService _cardio;

    public RecordedRunService(ModelContext context, ICardioReportService cardio)
    {
        _context = context;
        _cardio = cardio;
    }

    public Task<Result<RecordedRunView, Problem>> GetAsync(IdentityUser user, Guid trainingSessionId, CancellationToken ct) =>
        OwnedRunAsync(Guid.Parse(user.Id), trainingSessionId, ct)
            .BindAsync(run => _cardio.GetReportAsync(user, new DayRange(run.Session.Date, run.Session.Date), ct)
                .MapAsync(report => ReadoutAsync(run, report.MaxHeartRate, ct)));

    [Tool(Name = "GetRecordedRuns",
        Description = "The athlete's runs imported from Apple Health (recorded by an app such as Strava) in a range of days, oldest first, one block of text per run: start, recording app, distance, time, moving time, pace and moving pace per km, climb and descent; heart rate (average, peak, how much of the run it covers, minutes in the five zones and Edwards load) and whose it is — the recording app's, or the Oura ring's all-day series where the app wrote none; and splits per km with pace, climb, descent and average heart rate. No route or location is ever given. At most 92 days per call — a longer range returns its most recent 92 days.")]
    public async Task<IReadOnlyList<string>> GetRecordedRunsForChatAsync(IdentityUser user, DayRange range) =>
        await RunsInAsync(user, Recent(range), CancellationToken.None)
            .MatchAsync(runs => runs.Select(RunFacts.Summary).ToList(), _ => []);

    // ---- Reads ----

    private sealed record ImportedRun(CompletedTrainingSession Session, SessionSource.AppleHealth Source, WorkoutRecording Recording);

    private async Task<Result<ImportedRun, Problem>> OwnedRunAsync(Guid userId, Guid trainingSessionId, CancellationToken ct)
    {
        var session = await _context.Sessions.OfType<CompletedTrainingSession>()
            .AsNoTracking()
            .FirstOrDefaultAsync(held => held.TrainingSessionId == trainingSessionId && held.UserId == userId, ct);
        var recording = session is null
            ? null
            : await _context.WorkoutRecordings.AsNoTracking()
                .FirstOrDefaultAsync(held => held.TrainingSessionId == trainingSessionId, ct);

        return session is { Source: SessionSource.AppleHealth source } && recording is not null
            ? Result<ImportedRun, Problem>.Success(new ImportedRun(session, source, recording))
            : Result<ImportedRun, Problem>.Failure(new Problem.NotFound("No run imported from Apple Health with that id."));
    }

    private Task<Result<IReadOnlyList<RecordedRunView>, Problem>> RunsInAsync(
        IdentityUser user, DayRange range, CancellationToken ct) =>
        _cardio.GetReportAsync(user, range, ct)
            .MapAsync(async report => await ReadoutsAsync(
                await ImportedInAsync(Guid.Parse(user.Id), range, ct), report.MaxHeartRate, ct));

    /// <remarks>One after another: they share this context, which is not safe to use from two reads at once.</remarks>
    private async Task<IReadOnlyList<RecordedRunView>> ReadoutsAsync(
        IReadOnlyList<ImportedRun> runs, MaxHeartRate? maximum, CancellationToken ct)
    {
        var readouts = new List<RecordedRunView>(runs.Count);
        foreach (var run in runs) readouts.Add(await ReadoutAsync(run, maximum, ct));
        return readouts;
    }

    private async Task<IReadOnlyList<ImportedRun>> ImportedInAsync(Guid userId, DayRange range, CancellationToken ct)
    {
        var (from, to) = (
            new DateTimeOffset(range.From.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(-1).UtcDateTime,
            new DateTimeOffset(range.To.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero).AddDays(1).UtcDateTime);

        var sessions = (await _context.Sessions.OfType<CompletedTrainingSession>()
                .AsNoTracking()
                .Where(session => session.UserId == userId
                                  && EF.Property<Guid?>(session, RemodelConfiguration.HealthWorkoutIdColumn) != null
                                  && EF.Property<DateTime>(session, "StartTime") >= from
                                  && EF.Property<DateTime>(session, "StartTime") <= to)
                .ToListAsync(ct))
            .Where(session => session.Date >= range.From && session.Date <= range.To)
            .OrderBy(session => session.Period.StartTimeUtc)
            .ToList();

        var ids = sessions.Select(session => session.TrainingSessionId).ToList();
        var recordings = await _context.WorkoutRecordings.AsNoTracking()
            .Where(recording => ids.Contains(recording.TrainingSessionId))
            .ToDictionaryAsync(recording => recording.TrainingSessionId, ct);

        return
        [
            .. sessions
                .Where(session => session is { Source: SessionSource.AppleHealth } && recordings.ContainsKey(session.TrainingSessionId))
                .Select(session => new ImportedRun(
                    session, (SessionSource.AppleHealth)session.Source, recordings[session.TrainingSessionId]))
        ];
    }

    private async Task<RecordedRunView> ReadoutAsync(ImportedRun run, MaxHeartRate? maximum, CancellationToken ct) =>
        RunReadouts.Of(run.Session, run.Source, run.Recording, await HeartRateAsync(run, ct), maximum);

    /// <summary>The recording app's heart rate, or the ring's series over the run's clock time where it wrote none.</summary>
    private async Task<RunHeartRate> HeartRateAsync(ImportedRun run, CancellationToken ct)
    {
        if (run.Recording.HeartRate.IsRecorded)
            return new RunHeartRate(run.Recording.HeartRate.Samples, HeartRateSource.AppleHealth);

        var (start, end) = SessionMatching.Span(run.Session.Period) ?? (run.Session.Period.StartTime, run.Session.Period.StartTime);
        return new RunHeartRate(
            await _context.OuraHeartRateDays.SamplesAsync(run.Session.UserId, new TimeWindow(start, end), ct),
            HeartRateSource.Ring);
    }

    private static DayRange Recent(DayRange range) =>
        range.Days > ChatDayLimit ? new DayRange(range.To.AddDays(1 - ChatDayLimit), range.To) : range;
}
