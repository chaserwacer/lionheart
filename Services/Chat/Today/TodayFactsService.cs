using System.Globalization;
using lionheart.Data;
using lionheart.Functional;
using lionheart.Model;
using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Model.Training;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services.Cardio;
using lionheart.Services.Training.Availability;
using lionheart.Model.Training.Balance;
using lionheart.Model.Training.Load;
using lionheart.Model.Training.Score;
using lionheart.Services.Training.Balance;
using lionheart.Services.Training.Expectations;
using lionheart.Services.Training.Load;
using lionheart.Services.Training.Patterns;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Chat.Today;

/// <summary>The facts the briefing renders, for a reader that judges them rather than states them.</summary>
public interface ITodayFacts
{
    /// <summary>The athlete's day as of now. Throws only where the database cannot be read.</summary>
    Task<TodayFacts> ReadAsync(IdentityUser user, CancellationToken ct);

    /// <summary>The athlete's day as of the moment given, so a reader that has read the clock reads the facts at the same moment.</summary>
    Task<TodayFacts> ReadAsync(IdentityUser user, AthleteNow now, CancellationToken ct);
}

/// <summary>Reads the facts <see cref="TodayBriefing"/> renders, fresh for every message.</summary>
/// <remarks>
/// Not cached, unlike the context card, because the questions it answers ("did I train today", "how
/// did I sleep") are exactly the ones a cached copy gets wrong an hour after the athlete logs a
/// session or the ring syncs a night. It is not cheap — the training log and a week of cardio — and a
/// message reads it three times (the briefing, then the score's and next session's going-in
/// ratings), still well under a second against a model call of tens of seconds. The score and what
/// is up next are the briefing's own reads (<see cref="TodayBriefingService"/>), not facts: the
/// going-in rating reads these facts, and the score reads the going-in rating.
/// </remarks>
public sealed class TodayFactsService(
    ModelContext context,
    ICardioReportService cardio,
    MovementPatternMap map,
    TimeProvider clock,
    ILogger<TodayFactsService> logger) : ITodayFacts
{
    /// <summary>The nights the sleep baseline is drawn from.</summary>
    private const int BaselineNights = 60;

    /// <summary>The days readiness's usual range is drawn from, and the fewest that make one.</summary>
    private const int UsualRangeDays = 90;
    private const int FewestUsualDays = 30;

    private readonly ModelContext _context = context;
    private readonly ICardioReportService _cardio = cardio;
    private readonly MovementPatternMap _map = map;
    private readonly TimeProvider _clock = clock;
    private readonly ILogger _logger = logger;

    public Task<TodayFacts> ReadAsync(IdentityUser user, CancellationToken ct) => ReadAsync(user, AthleteNow.Of(_clock), ct);

    public async Task<TodayFacts> ReadAsync(IdentityUser user, AthleteNow now, CancellationToken ct)
    {
        var userId = Guid.Parse(user.Id);
        var today = now.Today;
        var from = now.WeekStart < now.Yesterday ? now.WeekStart : now.Yesterday;

        var log = await _context.ReadAsync(userId, today, _map, ct);
        var cardio = await CardioAsync(user, new DayRange(from, today), ct);
        var nights = await NightsAsync(userId, today, ct);

        return new TodayFacts(
            now,
            await SessionsAsync(log, from, today, cardio.LoggedSessionIds, ct),
            cardio.Days,
            Load(log, today),
            [.. nights.Where(night => night.Day >= today.AddDays(-1)).OrderByDescending(night => night.Day)],
            Baseline(nights, today),
            await ReadinessAsync(userId, today, ct),
            await MeansAsync(userId, today, ct),
            await WellnessAsync(userId, today, ct),
            await InjuriesAsync(userId, ct),
            await PlannedAsync(userId, log, today, ct),
            await NonTrainingAsync(userId, today, ct),
            await _context.EverydayAsync(userId, new DayRange(now.Yesterday, today), today, ct));
    }

    // ---- Training ----

    /// <summary>
    /// The completed sessions in the window, from the training log the score reads, with their
    /// clock times. A session of only cardio sets that the cardio report already counts is left to
    /// the report's line, which carries its heart rate.
    /// </summary>
    private async Task<IReadOnlyList<SessionFact>> SessionsAsync(
        TrainingLog log, DateOnly from, DateOnly today, IReadOnlySet<Guid> countedAsCardio, CancellationToken ct)
    {
        var logged = log.Between(from, today).ToList();
        var ids = logged.Select(session => session.TrainingSessionId).ToList();
        var sessions = (await _context.Sessions.OfType<CompletedTrainingSession>()
                .AsNoTracking()
                .Where(session => ids.Contains(session.TrainingSessionId))
                .ToListAsync(ct))
            .ToDictionary(session => session.TrainingSessionId);

        return [.. logged
            .Where(entry => sessions.ContainsKey(entry.TrainingSessionId))
            .Where(entry => entry.HardSets.Count > 0 || !countedAsCardio.Contains(entry.TrainingSessionId))
            .Select(entry => Session(log, entry, sessions[entry.TrainingSessionId]))
            .OrderBy(session => session.Start)];
    }

    private static SessionFact Session(TrainingLog log, SessionInLog entry, CompletedTrainingSession session) =>
        new(entry.Day,
            session.Period.StartTime,
            session.Period is Interval interval ? interval.Duration : TimeSpan.Zero,
            [.. entry.HardSets
                .GroupBy(set => set.MovementDataId)
                .Select(sets => new LiftFact(
                    log.NameOf(sets.Key),
                    log.ClassificationOf(sets.Key) is ClassificationStanding.Classified classified ? classified.Family : null,
                    sets.Count(),
                    TopSet(sets.MaxBy(set => (log.LoadOf(set, entry.Day), set.Reps))!)))],
            session.Sets.Any(set => set is DistanceTimeSet or TrailRunSet),
            session.Effort is EffortReported difficulty ? difficulty.Ratings.DifficultyRating : null,
            session.Effort is EffortReported fatigue ? fatigue.Ratings.AccumulatedFatigue : null);

    private static string TopSet(LiftSet set) => $"{Imperial.Weight(set.Weight, set.Unit)} × {set.Reps}";

    /// <summary>The last seven days against the four blocks before them: the one definition of load (<see cref="LoadRatio"/>).</summary>
    private static LoadFact Load(TrainingLog log, DateOnly today)
    {
        var week = VolumeTargets.WindowEndingOn(today);
        int Sessions(DayRange block) => log.Between(block.From, block.To).Count();
        return new LoadFact(
            LoadAnalysis.HardSetRatio(HardSetCounts.Of(log), week),
            LoadRatio.Of(Sessions(week), WeekExpectationsAnalysis.Before(week).Take(Progression.ChronicWeeks).Select(block => (double?)Sessions(block))));
    }

    /// <summary>
    /// The window's cardio, or that it could not be read: the rest of the briefing still stands, and
    /// says which.
    /// </summary>
    private Task<CardioRead> CardioAsync(IdentityUser user, DayRange range, CancellationToken ct) =>
        _cardio.GetReportAsync(user, range, ct).MatchAsync(
            report => new CardioRead(
                new CardioDays.Read([.. report.Sessions.Select(Cardio)], report.MaxHeartRate),
                report.Sessions
                    .Select(session => session.Source)
                    .OfType<CardioSessionSource.LoggedSession>()
                    .Select(logged => logged.TrainingSessionId)
                    .ToHashSet()),
            problem =>
            {
                _logger.LogWarning("The briefing could not read cardio: {Problem}", problem.Description);
                return new CardioRead(new CardioDays.Unread(), new HashSet<Guid>());
            });

    /// <param name="LoggedSessionIds">Logged sessions the report counted as cardio, so they are listed once.</param>
    private sealed record CardioRead(CardioDays Days, IReadOnlySet<Guid> LoggedSessionIds);

    private static CardioFact Cardio(CardioSession session) =>
        new(DateOnly.FromDateTime(session.Start.DateTime),
            session.Start,
            session.Minutes,
            session.Source switch
            {
                CardioSessionSource.RingWorkout ring => ring.Activity.Replace('_', ' '),
                CardioSessionSource.LoggedSession { IsTrailRun: true } => "logged run",
                _ => "logged cardio",
            },
            session.HeartRate is SessionHeartRate.Recorded recorded ? recorded.AverageBpm : null,
            session.HeartRate is SessionHeartRate.Recorded { Zones: { } zones } ? zones.Zone2 : null,
            session.HeartRate is SessionHeartRate.Recorded { Zones: { } fast } ? fast.Zone4 + fast.Zone5 : null);

    private async Task<IReadOnlyList<PlannedFact>> PlannedAsync(Guid userId, TrainingLog log, DateOnly today, CancellationToken ct) =>
        [.. (await _context.Sessions.OfType<PlannedTrainingSession>()
                .AsNoTracking()
                .Where(session => session.UserId == userId)
                .ToListAsync(ct))
            .Where(plan => plan.PlannedFor >= today && plan.PlannedFor <= today.AddDays(6))
            .OrderBy(plan => plan.PlannedFor)
            .Select(plan => new PlannedFact(
                plan.PlannedFor,
                [.. plan.Prescriptions.Select(prescription => Prescribed(prescription, log)).Distinct()]))];

    private static string Prescribed(SetPrescription prescription, TrainingLog log) => prescription switch
    {
        LiftPrescription lift => log.NameOf(lift.MovementDataId),
        DistanceTimePrescription distance => log.NameOf(distance.MovementDataId),
        FreeformPrescription freeform => freeform.Text,
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private async Task<IReadOnlyList<NonTrainingFact>> NonTrainingAsync(Guid userId, DateOnly today, CancellationToken ct) =>
        [.. (await _context.AvailabilityAsync(userId, new DayRange(today, today.AddDays(6)), ct))
            .Unavailable
            .Select(day => new NonTrainingFact(day.Day, day.Note))];

    // ---- Recovery ----

    /// <summary>Main sleeps from the baseline's first night through today, as Oura filed them.</summary>
    private async Task<IReadOnlyList<NightFact>> NightsAsync(Guid userId, DateOnly today, CancellationToken ct)
    {
        var first = today.AddDays(-BaselineNights - 1);
        var periods = await _context.OuraSleepPeriods
            .AsNoTracking()
            .Where(period => period.UserId == userId && period.Day >= first && period.Day <= today)
            .ToListAsync(ct);
        var scores = await _context.OuraDailySleep
            .AsNoTracking()
            .Where(sleep => sleep.UserId == userId && sleep.Day >= today.AddDays(-1) && sleep.Day <= today)
            .ToDictionaryAsync(sleep => sleep.Day, sleep => sleep.Reading.Score, ct);

        return [.. periods
            .Where(period => period.Reading.IsMainSleep)
            .GroupBy(period => period.Day)
            .Select(day => day.MaxBy(period => period.Reading.TotalSleepSeconds)!)
            .Select(period => new NightFact(
                period.Day,
                period.Reading.Period.StartTime,
                period.Reading.Period is Interval interval ? interval.End : period.Reading.Period.StartTime,
                period.Reading.TotalSleepSeconds,
                period.Reading.Efficiency,
                period.Reading.LowestHeartRate,
                period.Reading.AverageHrv,
                scores.GetValueOrDefault(period.Day)))];
    }

    /// <summary>The nights before the two the briefing states, so neither is measured against itself.</summary>
    private static NightBaseline Baseline(IReadOnlyList<NightFact> nights, DateOnly today)
    {
        var before = nights.Where(night => night.Day < today.AddDays(-1)).ToList();
        return new NightBaseline(
            Mean(before.Select(night => night.AverageHrv)),
            Mean(before.Select(night => night.LowestHeartRate)),
            before.Count);
    }

    private async Task<ReadinessFact?> ReadinessAsync(Guid userId, DateOnly today, CancellationToken ct)
    {
        var days = await _context.OuraDailyReadiness
            .AsNoTracking()
            .Where(day => day.UserId == userId && day.Day >= today.AddDays(-UsualRangeDays) && day.Day <= today)
            .Select(day => new { day.Day, day.Reading })
            .ToListAsync(ct);

        if (days.FirstOrDefault(day => day.Day == today)?.Reading is not { } reading) return null;

        var usual = days.Where(day => day.Day < today && day.Reading.Score is not null)
            .Select(day => day.Reading.Score!.Value)
            .Order()
            .ToList();
        var ranged = usual.Count >= FewestUsualDays;

        return new ReadinessFact(
            reading.Score,
            reading.Contributors.HrvBalance,
            reading.Contributors.RestingHeartRate,
            reading.TemperatureDeviation,
            ranged ? Percentile(usual, 0.1) : null,
            ranged ? Percentile(usual, 0.9) : null);
    }

    private async Task<WeekMeans> MeansAsync(Guid userId, DateOnly today, CancellationToken ct)
    {
        var first = today.AddDays(-6);
        var readiness = await _context.OuraDailyReadiness.AsNoTracking()
            .Where(day => day.UserId == userId && day.Day >= first && day.Day <= today)
            .Select(day => day.Reading.Score)
            .ToListAsync(ct);
        var sleep = await _context.OuraDailySleep.AsNoTracking()
            .Where(day => day.UserId == userId && day.Day >= first && day.Day <= today)
            .Select(day => day.Reading.Score)
            .ToListAsync(ct);
        return new WeekMeans(Mean(readiness), Mean(sleep), Math.Max(readiness.Count, sleep.Count));
    }

    private async Task<WellnessFact?> WellnessAsync(Guid userId, DateOnly today, CancellationToken ct) =>
        await _context.WellnessStates.AsNoTracking()
            .Where(state => state.UserID == userId && state.Date >= today.AddDays(-6) && state.Date <= today)
            .OrderByDescending(state => state.Date)
            .Select(state => new WellnessFact(
                state.Date, state.OverallScore, state.MoodScore, state.EnergyScore, state.StressScore, state.MotivationScore))
            .FirstOrDefaultAsync(ct);

    private async Task<IReadOnlyList<InjuryFact>> InjuriesAsync(Guid userId, CancellationToken ct) =>
        await _context.Injuries.AsNoTracking()
            .Where(injury => injury.UserID == userId && injury.IsActive)
            .OrderBy(injury => injury.InjuryDate)
            .Select(injury => new InjuryFact(
                injury.Name,
                injury.InjuryEvents.OrderByDescending(e => e.CreationTime).Select(e => (int?)e.PainLevel).FirstOrDefault()))
            .ToListAsync(ct);

    /// <summary>A reading Oura did not compute is skipped, not averaged in as a zero.</summary>
    private static double? Mean(IEnumerable<int?> readings)
    {
        var present = readings.Where(reading => reading.HasValue).Select(reading => (double)reading!.Value).ToList();
        return present.Count == 0 ? null : present.Average();
    }

    /// <summary>Nearest-rank percentile of a sorted list, the rule the readiness notes use.</summary>
    private static int Percentile(IReadOnlyList<int> sorted, double fraction) =>
        sorted[(int)Math.Floor(fraction * (sorted.Count - 1))];
}
