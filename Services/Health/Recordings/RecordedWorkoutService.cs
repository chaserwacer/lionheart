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

namespace lionheart.Services.Health.Recordings;

public interface IRecordedWorkoutService
{
    /// <summary>One imported workout read whole, or not found for a session that is not one of the athlete's imported workouts.</summary>
    Task<Result<RecordedWorkoutView, Problem>> GetAsync(IdentityUser user, Guid trainingSessionId, CancellationToken ct);
}

/// <summary>
/// Reads the workouts imported from Apple Health back — runs, rides, hikes, walks: for each one's
/// own page, and as summaries for the chat model.
/// </summary>
/// <remarks>
/// The heart rate is the recording app's where it wrote any, and otherwise the ring's all-day
/// series over the same clock time, which the page labels; the maximum the zones are drawn from is
/// the cardio report's, so a workout reads the same here as there.
/// </remarks>
[ToolProvider]
public sealed class RecordedWorkoutService : IRecordedWorkoutService
{
    /// <summary>The most days one chat call reads, the same bound the cardio tool keeps.</summary>
    private const int ChatDayLimit = 92;

    private readonly ModelContext _context;
    private readonly ICardioReportService _cardio;

    public RecordedWorkoutService(ModelContext context, ICardioReportService cardio)
    {
        _context = context;
        _cardio = cardio;
    }

    public Task<Result<RecordedWorkoutView, Problem>> GetAsync(IdentityUser user, Guid trainingSessionId, CancellationToken ct) =>
        OwnedWorkoutAsync(Guid.Parse(user.Id), trainingSessionId, ct)
            .BindAsync(workout => _cardio.GetReportAsync(user, new DayRange(workout.Session.Date, workout.Session.Date), ct)
                .MapAsync(report => ReadoutAsync(workout, report.MaxHeartRate, ct)));

    [Tool(Name = "GetRecordedWorkouts",
        Description = "The athlete's workouts imported from Apple Health (recorded by an app such as Strava or a watch) in a range of days, oldest first, one block of text per workout: what it was (run, ride, indoor ride, hike, walk, indoor run), start, recording app, distance, time, moving time, pace per km (speed in km/h for a ride), climb and descent; heart rate (average, peak, how much of it the trace covers, minutes in the five zones and Edwards load) and whose it is — the recording app's, or the Oura ring's all-day series where the app wrote none; the app's own averages where it wrote them (power, cadence, speed, ground contact time, vertical oscillation, stride length) and the one-minute heart-rate recovery; and splits per km. No route or location is ever given. At most 92 days per call — a longer range returns its most recent 92 days.")]
    public async Task<IReadOnlyList<string>> GetRecordedWorkoutsForChatAsync(IdentityUser user, DayRange range) =>
        await WorkoutsInAsync(user, Recent(range), CancellationToken.None)
            .MatchAsync(workouts => workouts.Select(WorkoutFacts.Summary).ToList(), _ => []);

    // ---- Reads ----

    private sealed record ImportedWorkout(CompletedTrainingSession Session, SessionSource.AppleHealth Source, WorkoutRecording Recording);

    private async Task<Result<ImportedWorkout, Problem>> OwnedWorkoutAsync(Guid userId, Guid trainingSessionId, CancellationToken ct)
    {
        var session = await _context.Sessions.OfType<CompletedTrainingSession>()
            .AsNoTracking()
            .FirstOrDefaultAsync(held => held.TrainingSessionId == trainingSessionId && held.UserId == userId, ct);
        var recording = session is null
            ? null
            : await _context.WorkoutRecordings.AsNoTracking()
                .FirstOrDefaultAsync(held => held.TrainingSessionId == trainingSessionId, ct);

        return session is { Source: SessionSource.AppleHealth source } && recording is not null
            ? Result<ImportedWorkout, Problem>.Success(new ImportedWorkout(session, source, recording))
            : Result<ImportedWorkout, Problem>.Failure(new Problem.NotFound("No workout imported from Apple Health with that id."));
    }

    private Task<Result<IReadOnlyList<RecordedWorkoutView>, Problem>> WorkoutsInAsync(
        IdentityUser user, DayRange range, CancellationToken ct) =>
        _cardio.GetReportAsync(user, range, ct)
            .MapAsync(async report => await ReadoutsAsync(
                await ImportedInAsync(Guid.Parse(user.Id), range, ct), report.MaxHeartRate, ct));

    /// <remarks>One after another: they share this context, which is not safe to use from two reads at once.</remarks>
    private async Task<IReadOnlyList<RecordedWorkoutView>> ReadoutsAsync(
        IReadOnlyList<ImportedWorkout> workouts, MaxHeartRate? maximum, CancellationToken ct)
    {
        var readouts = new List<RecordedWorkoutView>(workouts.Count);
        foreach (var workout in workouts) readouts.Add(await ReadoutAsync(workout, maximum, ct));
        return readouts;
    }

    private async Task<IReadOnlyList<ImportedWorkout>> ImportedInAsync(Guid userId, DayRange range, CancellationToken ct)
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
                .Select(session => new ImportedWorkout(
                    session, (SessionSource.AppleHealth)session.Source, recordings[session.TrainingSessionId]))
        ];
    }

    private async Task<RecordedWorkoutView> ReadoutAsync(ImportedWorkout workout, MaxHeartRate? maximum, CancellationToken ct) =>
        WorkoutReadouts.Of(workout.Session, workout.Source, workout.Recording, await HeartRateAsync(workout, ct), maximum);

    /// <summary>The recording app's heart rate, or the ring's series over the workout's clock time where it wrote none.</summary>
    private async Task<WorkoutHeartRate> HeartRateAsync(ImportedWorkout workout, CancellationToken ct)
    {
        if (workout.Recording.HeartRate.IsRecorded)
            return new WorkoutHeartRate(workout.Recording.HeartRate.Samples, HeartRateSource.AppleHealth);

        var (start, end) = SessionMatching.Span(workout.Session.Period) ?? (workout.Session.Period.StartTime, workout.Session.Period.StartTime);
        return new WorkoutHeartRate(
            await _context.OuraHeartRateDays.SamplesAsync(workout.Session.UserId, new TimeWindow(start, end), ct),
            HeartRateSource.Ring);
    }

    private static DayRange Recent(DayRange range) =>
        range.Days > ChatDayLimit ? new DayRange(range.To.AddDays(1 - ChatDayLimit), range.To) : range;
}
