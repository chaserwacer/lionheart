using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Model.Oura;
using lionheart.Model.Oura.Readings;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;

namespace lionheart.Services.Cardio;

/// <summary>Everything the cardio report is computed from, already read.</summary>
/// <param name="Range">The days the report describes.</param>
/// <param name="RingWorkouts">Oura workouts from at least twelve weeks before the range's end and a year back for the peak.</param>
/// <param name="LoggedSessions">Completed Lionheart sessions over the same span.</param>
/// <param name="HeartRate">Every stored heart-rate sample on the UTC days those sessions touch, oldest first.</param>
/// <param name="MainSleeps">The night's main sleep per waking day, for resting heart rate.</param>
public sealed record CardioInputs(
    DayRange Range,
    int Age,
    IReadOnlyList<OuraWorkout> RingWorkouts,
    IReadOnlyList<CompletedTrainingSession> LoggedSessions,
    IReadOnlyList<HeartRateSample> HeartRate,
    IReadOnlyList<OuraVo2Max> Vo2Max,
    IReadOnlyList<OuraDailyCardiovascularAge> CardiovascularAge,
    IReadOnlyList<OuraSleepPeriod> MainSleeps);

/// <summary>
/// The cardio report as a function of what was read.
/// </summary>
/// <remarks>
/// Pure, so every rule in it — which workouts are sessions, how a gap in the series is counted,
/// which maximum the zones are drawn from — is tested without a database. The steps, in order:
/// gather the sessions, read each one's heart rate, settle the maximum, draw the zones, then sum
/// the weeks and compare the windows.
/// </remarks>
public static class CardioAnalysis
{
    /// <summary>How far back the ring's own peak heart rate is read from.</summary>
    public const int PeakLookBackDays = 365;

    /// <summary>
    /// How much of a ring workout must lie inside a logged session for the two to be one session.
    /// </summary>
    /// <remarks>
    /// The ring detects most of what the athlete logs, a few minutes late and a few minutes short.
    /// Half its own length inside the logged session is a match; anything less is two things that
    /// happened to be near each other.
    /// </remarks>
    public const double SameSessionOverlap = 0.5;

    /// <summary>The first day the report needs read: twelve weeks back for the windows, a year for the peak.</summary>
    public static DateOnly ReadFrom(DayRange range) =>
        new[]
        {
            range.From,
            range.To.AddDays(-7 * (AerobicBase.RecentWeeks + AerobicBase.BeforeWeeks) + 1),
            range.To.AddDays(1 - PeakLookBackDays),
        }.Min();

    public static CardioReport Build(CardioInputs inputs)
    {
        var gathered = Gather(inputs.RingWorkouts, inputs.LoggedSessions);
        var withSamples = gathered.Sessions
            .Select(session => (Session: session, Samples: Samples(inputs.HeartRate, session)))
            .ToList();

        var maximum = MaxHeartRate.Estimate(inputs.Age, Peak(withSamples, inputs.Range.To));
        var sessions = withSamples
            .Select(pair => pair.Session with { HeartRate = HeartRateOf(pair.Session, pair.Samples, maximum) })
            .OrderBy(session => session.Start)
            .ToList();

        var inRange = sessions.Where(session => Within(inputs.Range, LocalDay(session.Start))).ToList();

        return new CardioReport(
            inputs.Range,
            maximum,
            inRange,
            Weeks(inputs.Range, inRange),
            Points(inputs.Range, inputs.Vo2Max.Select(estimate => (estimate.Day, (double?)estimate.Reading.Vo2Max))),
            Points(inputs.Range, inputs.CardiovascularAge.Select(age => (age.Day, (double?)age.Reading.VascularAge))),
            Points(inputs.Range, inputs.MainSleeps.Select(night => (night.Day, RestingRate(night.Reading)))),
            AerobicBaseOf(inputs, sessions),
            [.. gathered.Excluded
                .Where(excluded => Within(inputs.Range, excluded.Workout.Day))
                .GroupBy(excluded => (excluded.Workout.Category, excluded.Reason))
                .OrderByDescending(group => group.Count())
                .Select(group => new ExcludedWorkouts(group.Key.Category, group.Count(), group.Key.Reason))]);
    }

    // ---- Gathering the sessions ----

    /// <summary>
    /// The cardio sessions among the logged sessions and the ring's workouts, with the ring's
    /// duplicates of logged sessions and its non-training workouts left out — before any heart rate
    /// is read. The habits report places these on the day.
    /// </summary>
    public static IReadOnlyList<CardioSession> Sessions(
        IReadOnlyList<OuraWorkout> ringWorkouts, IReadOnlyList<CompletedTrainingSession> logged) =>
        Gather(ringWorkouts, logged).Sessions;

    private sealed record Classified(OuraWorkout Source, WorkoutCategory Category, DateOnly Day);

    private sealed record Exclusion(Classified Workout, string Reason);

    private sealed record Gathering(IReadOnlyList<CardioSession> Sessions, IReadOnlyList<Exclusion> Excluded);

    /// <summary>
    /// The logged cardio sessions, then every ring workout that is cardio and is not one of them.
    /// </summary>
    /// <remarks>
    /// A ring workout inside a logged lifting session is that lifting session as the ring saw it —
    /// most of the ring's handball and "other" are the gym — so it is excluded rather than counted
    /// as cardio. Inside a logged run, it is the run, already counted.
    /// </remarks>
    private static Gathering Gather(
        IReadOnlyList<OuraWorkout> ringWorkouts, IReadOnlyList<CompletedTrainingSession> logged)
    {
        var loggedCardio = logged.Where(IsCardio).ToList();
        var loggedLifting = logged.Where(session => !IsCardio(session)).ToList();

        var fromLog = loggedCardio
            .Select(session => Span(session.Period) is { } span
                ? new CardioSession(
                    new CardioSessionSource.LoggedSession(session.TrainingSessionId, session.Sets.OfType<TrailRunSet>().Any()),
                    span.Start,
                    span.End,
                    DistanceMeters(session),
                    new SessionHeartRate.NotRecorded())
                : null)
            .OfType<CardioSession>()
            .ToList();

        var decided = ringWorkouts
            .Select(workout => new Classified(workout, WorkoutCategories.Of(workout.Reading.Activity), workout.Day))
            .Select(workout => (Workout: workout, Reason: ExclusionReason(workout, loggedCardio, loggedLifting)))
            .ToList();

        var fromRing = decided
            .Where(decision => decision.Reason is null)
            .Select(decision => RingSession(decision.Workout))
            .OfType<CardioSession>();

        return new Gathering(
            [.. fromLog, .. fromRing],
            [.. decided
                .Where(decision => decision.Reason is not null)
                .Select(decision => new Exclusion(decision.Workout, decision.Reason!))]);
    }

    private static bool IsCardio(CompletedTrainingSession session) =>
        session.Sets.Any(set => set is TrailRunSet or DistanceTimeSet);

    private static double? DistanceMeters(CompletedTrainingSession session)
    {
        var distances = session.Sets
            .Select(set => set switch
            {
                TrailRunSet run => run.Distance * run.Unit.MetersEach(),
                DistanceTimeSet timed => timed.Distance * timed.Unit.MetersEach(),
                _ => (double?)null,
            })
            .OfType<double>()
            .ToList();

        return distances.Count is 0 ? null : distances.Sum();
    }

    /// <summary>Why a ring workout is not a cardio session of its own, or nothing when it is one.</summary>
    private static string? ExclusionReason(
        Classified workout,
        IReadOnlyList<CompletedTrainingSession> loggedCardio,
        IReadOnlyList<CompletedTrainingSession> loggedLifting)
    {
        if (Span(workout.Source.Period) is not { } span) return "no end time";
        var (start, end) = span;
        if (loggedCardio.Any(session => Overlaps(start, end, session.Period))) return "the same session as a logged run";
        if (loggedLifting.Any(session => Overlaps(start, end, session.Period))) return "inside a logged lifting session";

        return WorkoutCategories.IsCardioSession(workout.Category, end - start)
            ? null
            : workout.Category switch
            {
                WorkoutCategory.Walk => $"a walk under {WorkoutCategories.SessionWalk.TotalMinutes:0} minutes",
                WorkoutCategory.Strength => "strength work",
                WorkoutCategory.Incidental => "not training",
                WorkoutCategory.Unrecognised => "an activity not yet classified",
                WorkoutCategory.Endurance or WorkoutCategory.Sport => throw new System.Diagnostics.UnreachableException(),
                _ => throw new System.Diagnostics.UnreachableException(),
            };
    }

    private static CardioSession? RingSession(Classified workout) =>
        Span(workout.Source.Period) is { } span
            ? new CardioSession(
                new CardioSessionSource.RingWorkout(workout.Source.OuraId, workout.Source.Reading.Activity, workout.Category),
                span.Start,
                span.End,
                workout.Source.Reading.DistanceMeters,
                new SessionHeartRate.NotRecorded())
            : null;

    private static bool Overlaps(DateTimeOffset start, DateTimeOffset end, Period logged)
    {
        if (Span(logged) is not { } loggedSpan) return false;
        var (loggedStart, loggedEnd) = loggedSpan;

        var shared = Min(end, loggedEnd) - Max(start, loggedStart);
        var own = end - start;
        return own > TimeSpan.Zero && shared.TotalSeconds >= SameSessionOverlap * own.TotalSeconds;
    }

    private static (DateTimeOffset Start, DateTimeOffset End)? Span(Period period) => period switch
    {
        Interval interval => (interval.Start, interval.End),
        Instant => null,
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left < right ? left : right;
    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right) => left > right ? left : right;

    // ---- Heart rate across a session ----

    private static IReadOnlyList<HeartRateSample> Samples(IReadOnlyList<HeartRateSample> all, CardioSession session) =>
        [.. all.Where(sample => sample.At >= session.Start && sample.At < session.End)];

    /// <summary>The ring's peak across the cardio sessions of the year ending on <paramref name="lastDay"/>.</summary>
    private static int? Peak(
        IEnumerable<(CardioSession Session, IReadOnlyList<HeartRateSample> Samples)> sessions, DateOnly lastDay)
    {
        var firstDay = lastDay.AddDays(1 - PeakLookBackDays);
        return MaxHeartRate.PeakOf(
        [
            .. sessions
                .Where(pair => LocalDay(pair.Session.Start) is var day && day >= firstDay && day <= lastDay)
                .SelectMany(pair => pair.Samples)
                .Select(sample => sample.Bpm)
        ]);
    }

    /// <summary>
    /// What the series says about one session.
    /// </summary>
    /// <remarks>
    /// The average is weighted by the time each sample stands for, so a burst of fast samples at
    /// the end of a run does not outvote the hour of five-minute samples before it.
    /// </remarks>
    public static SessionHeartRate HeartRateOf(
        CardioSession session, IReadOnlyList<HeartRateSample> samples, MaxHeartRate? maximum)
    {
        if (samples.Count is 0) return new SessionHeartRate.NotRecorded();

        var attributed = SampleTime.Attributed(samples, session.End).ToList();
        var minutes = attributed.Sum(pair => pair.Minutes);
        var average = minutes > 0
            ? attributed.Sum(pair => pair.Sample.Bpm * pair.Minutes) / minutes
            : samples.Average(sample => sample.Bpm);

        return new SessionHeartRate.Recorded(
            (int)Math.Round(average),
            samples.Max(sample => sample.Bpm),
            session.Minutes > 0 ? Math.Min(1, minutes / session.Minutes) : 0,
            maximum is null
                ? null
                : attributed.Aggregate(
                    ZoneMinutes.None,
                    (zones, pair) => zones + ZoneMinutes.InZone(ZoneMinutes.ZoneOf(pair.Sample.Bpm, maximum.Bpm), pair.Minutes)));
    }

    private static ZoneMinutes ZonesOf(CardioSession session) =>
        session.HeartRate is SessionHeartRate.Recorded { Zones: { } zones } ? zones : ZoneMinutes.None;

    // ---- Weeks and windows ----

    /// <summary>Every Monday-to-Sunday week the range touches, with the sessions that began in it.</summary>
    private static IReadOnlyList<CardioWeek> Weeks(DayRange range, IReadOnlyList<CardioSession> sessions)
    {
        var first = WeekStarting(range.From);
        var count = (WeekStarting(range.To).DayNumber - first.DayNumber) / 7 + 1;
        var byWeek = sessions.ToLookup(session => WeekStarting(LocalDay(session.Start)));

        return
        [
            .. Enumerable.Range(0, count)
                .Select(index => first.AddDays(7 * index))
                .Select(week => new CardioWeek(
                    week,
                    byWeek[week].Count(),
                    byWeek[week].Sum(session => session.Minutes),
                    byWeek[week].Aggregate(ZoneMinutes.None, (zones, session) => zones + ZonesOf(session))))
        ];
    }

    public static DateOnly WeekStarting(DateOnly day) =>
        day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    private static AerobicBase AerobicBaseOf(CardioInputs inputs, IReadOnlyList<CardioSession> sessions)
    {
        var recent = new DayRange(inputs.Range.To.AddDays(1 - 7 * AerobicBase.RecentWeeks), inputs.Range.To);
        var before = new DayRange(
            recent.From.AddDays(-7 * AerobicBase.BeforeWeeks), recent.From.AddDays(-1));

        Comparison Compare(Func<DayRange, double?> figure) => new(figure(recent), figure(before));

        IReadOnlyList<CardioSession> In(DayRange window) =>
            [.. sessions.Where(session => Within(window, LocalDay(session.Start)))];

        double? PerWeek(DayRange window, Func<CardioSession, double> minutes) =>
            In(window).Sum(minutes) / (window.Days / 7.0);

        // No session with a heart rate in a window that has sessions means the series is not
        // synced that far back, which is not the same as no time in zone 2.
        double? Zone2(DayRange window) =>
            In(window) is { Count: > 0 } held && held.All(session => session.HeartRate is SessionHeartRate.NotRecorded)
                ? null
                : PerWeek(window, session => ZonesOf(session).Zone2);

        double? Mean(DayRange window, IEnumerable<(DateOnly Day, double? Value)> values)
        {
            var held = values.Where(value => Within(window, value.Day)).Select(value => value.Value).OfType<double>().ToList();
            return held.Count is 0 ? null : held.Average();
        }

        return new AerobicBase(
            recent,
            before,
            Compare(Zone2),
            Compare(window => PerWeek(window, session => session.Minutes)),
            Compare(window => Mean(window, inputs.Vo2Max.Select(estimate => (estimate.Day, (double?)estimate.Reading.Vo2Max)))),
            Compare(window => Mean(window, inputs.MainSleeps.Select(night => (night.Day, RestingRate(night.Reading))))),
            Compare(window => Mean(window, inputs.CardiovascularAge.Select(age => (age.Day, (double?)age.Reading.VascularAge)))));
    }

    /// <summary>
    /// The night's lowest heart rate as a resting rate, or nothing where the ring took no reading.
    /// </summary>
    /// <remarks>A lowest rate of zero is a period with no reading, not a heart rate — the rule the night's own screen applies.</remarks>
    private static double? RestingRate(SleepPeriodReading night) =>
        night.LowestHeartRate is > 0 and var lowest ? lowest : null;

    private static IReadOnlyList<DayValue> Points(DayRange range, IEnumerable<(DateOnly Day, double? Value)> values) =>
    [
        .. values
            .Where(value => Within(range, value.Day) && value.Value is not null)
            .GroupBy(value => value.Day)
            .OrderBy(day => day.Key)
            .Select(day => new DayValue(day.Key, day.First().Value!.Value))
    ];

    /// <summary>The day a session began on the athlete's own clock, at the offset it was recorded with.</summary>
    private static DateOnly LocalDay(DateTimeOffset instant) => DateOnly.FromDateTime(instant.DateTime);

    private static bool Within(DayRange range, DateOnly day) => day >= range.From && day <= range.To;
}
