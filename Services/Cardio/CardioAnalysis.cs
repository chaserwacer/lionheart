using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Model.Health;
using lionheart.Model.Oura;
using lionheart.Model.Oura.Readings;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;

namespace lionheart.Services.Cardio;

/// <summary>Everything the cardio report is computed from, already read.</summary>
/// <param name="Range">The days the report describes.</param>
/// <param name="Sessions">The gathered sessions and exclusions from twelve weeks before the range's end, and a year back for the peak.</param>
/// <param name="HeartRate">Every stored heart-rate sample on the UTC days those sessions touch, oldest first.</param>
/// <param name="RecordedHeartRate">
/// The heart rate the recording app wrote to Apple Health, by the logged session it was imported as —
/// read for that session ahead of the ring's series.
/// </param>
/// <param name="MainSleeps">The night's main sleep per waking day, for resting heart rate.</param>
public sealed record CardioInputs(
    DayRange Range,
    int Age,
    CardioGathering Sessions,
    IReadOnlyList<HeartRateSample> HeartRate,
    IReadOnlyDictionary<Guid, RecordedHeartRate> RecordedHeartRate,
    IReadOnlyList<OuraVo2Max> Vo2Max,
    IReadOnlyList<OuraDailyCardiovascularAge> CardiovascularAge,
    IReadOnlyList<OuraSleepPeriod> MainSleeps)
{
    /// <summary>The Apple Watch's VO2max estimates from Health, one per day — kept apart from Oura's.</summary>
    public IReadOnlyList<DayValue> AppleWatchVo2Max { get; init; } = [];
}

/// <summary>A ring workout left out of the sessions, the day Oura filed it under, and why.</summary>
public sealed record Exclusion(DateOnly Day, WorkoutCategory Category, WorkoutExclusion Reason);

/// <summary>The cardio sessions among the logged sessions and the ring's workouts, and what was left out.</summary>
public sealed record CardioGathering(IReadOnlyList<GatheredSession> Sessions, IReadOnlyList<Exclusion> Excluded);

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
    /// The share of a window's session time that must have heart rate behind it for the window's
    /// zone-2 minutes to mean anything.
    /// </summary>
    /// <remarks>
    /// Below it, the window is mostly sessions the series was never synced for, and its zone 2
    /// would read low for want of data — which, set against a recent window that has it, looks like
    /// improvement.
    /// </remarks>
    public const double ZoneCoverageFloor = 0.5;

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
        var withSamples = inputs.Sessions.Sessions
            .Select(session => (Session: session, Read: HeartRateRead(inputs, session)))
            .ToList();

        var maximum = MaxHeartRate.Estimate(
            inputs.Age, Peak(withSamples.Select(pair => (pair.Session, pair.Read.Samples)), inputs.Range.To));
        var sessions = withSamples
            .Select(pair => new CardioSession(
                pair.Session.Source,
                pair.Session.Start,
                pair.Session.End,
                pair.Session.DistanceMeters,
                HeartRateOf(pair.Session, pair.Read.Samples, pair.Read.Source, maximum)))
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
            AerobicBaseOf(inputs, sessions, maximum),
            [.. inputs.Sessions.Excluded
                .Where(excluded => Within(inputs.Range, excluded.Day))
                .GroupBy(excluded => (excluded.Category, excluded.Reason))
                .OrderByDescending(group => group.Count())
                .Select(group => new ExcludedWorkouts(group.Key.Category, group.Key.Reason, group.Count()))])
        {
            AppleWatchVo2Max = Points(inputs.Range, inputs.AppleWatchVo2Max.Select(estimate => (estimate.Day, (double?)estimate.Value))),
        };
    }

    // ---- Gathering the sessions ----

    /// <summary>
    /// The logged cardio sessions, then every ring workout that is cardio and is not one of them.
    /// </summary>
    /// <remarks>
    /// A ring workout inside a logged lifting session is that lifting session as the ring saw it, so
    /// it is excluded rather than counted as cardio. Inside a logged run, it is the run, already
    /// counted (<see cref="SessionMatching"/>). A logged walk shorter than a session walk is left
    /// out as the ring's would be.
    /// </remarks>
    public static CardioGathering Gather(
        IReadOnlyList<OuraWorkout> ringWorkouts, IReadOnlyList<CompletedTrainingSession> logged)
    {
        var cardio = logged.Where(SessionMatching.IsCardio).ToList();
        var shortWalks = cardio.Where(IsShortWalk).ToList();
        var fromLog = cardio
            .Except(shortWalks)
            .Select(session => SessionMatching.Span(session.Period) is { } span
                ? new GatheredSession(
                    new CardioSessionSource.LoggedSession(
                        session.TrainingSessionId,
                        session.Sets.OfType<TrailRunSet>().Any(),
                        session.Source is SessionSource.AppleHealth,
                        session.Sets.EnduranceActivity),
                    span.Start,
                    span.End,
                    DistanceMeters(session))
                : null)
            .OfType<GatheredSession>();

        var decided = ringWorkouts
            .Select(workout => Decide(workout, logged))
            .ToList();

        return new CardioGathering(
            [.. fromLog, .. decided.OfType<RingDecision.Session>().Select(decision => decision.Gathered)],
            [
                .. shortWalks.Select(walk => new Exclusion(walk.Date, WorkoutCategory.Walk, WorkoutExclusion.ShortWalk)),
                .. decided.OfType<RingDecision.Excluded>().Select(decision => decision.Exclusion),
            ]);
    }

    /// <summary>
    /// A logged session that is only walking, shorter than a walk has to be to count as a session —
    /// a watch's walk held to the rule the ring's walks are (<see cref="WorkoutCategories.IsCardioSession"/>).
    /// </summary>
    private static bool IsShortWalk(CompletedTrainingSession session) =>
        session.Sets.Where(set => set is TrailRunSet or EnduranceSet or DistanceTimeSet)
            .All(set => set is EnduranceSet { Activity: EnduranceActivity.Walk })
        && SessionMatching.Span(session.Period) is { } span
        && !WorkoutCategories.IsCardioSession(WorkoutCategory.Walk, span.End - span.Start);

    /// <summary>What becomes of one ring workout: a session of its own, or excluded for a reason.</summary>
    private abstract record RingDecision
    {
        private RingDecision() { }

        public sealed record Session(GatheredSession Gathered) : RingDecision;

        public sealed record Excluded(Exclusion Exclusion) : RingDecision;
    }

    private static RingDecision Decide(OuraWorkout workout, IReadOnlyList<CompletedTrainingSession> logged)
    {
        var category = WorkoutCategories.Of(workout.Reading.Activity);
        RingDecision Excluded(WorkoutExclusion reason) =>
            new RingDecision.Excluded(new Exclusion(workout.Day, category, reason));

        if (SessionMatching.Span(workout.Period) is not { } span) return Excluded(WorkoutExclusion.NoDuration);

        switch (SessionMatching.Find(span, logged))
        {
            case SessionMatch.LoggedRun: return Excluded(WorkoutExclusion.SameAsLoggedRun);
            case SessionMatch.LoggedLifting: return Excluded(WorkoutExclusion.InsideLoggedLifting);
            case SessionMatch.None: break;
            default: throw new System.Diagnostics.UnreachableException();
        }

        if (WorkoutCategories.IsCardioSession(category, span.End - span.Start))
        {
            return new RingDecision.Session(new GatheredSession(
                new CardioSessionSource.RingWorkout(workout.OuraId, workout.Reading.Activity, category),
                span.Start,
                span.End,
                workout.Reading.DistanceMeters));
        }

        return Excluded(category switch
        {
            WorkoutCategory.Walk => WorkoutExclusion.ShortWalk,
            WorkoutCategory.Strength => WorkoutExclusion.Strength,
            WorkoutCategory.Incidental => WorkoutExclusion.NotTraining,
            WorkoutCategory.Unrecognised => WorkoutExclusion.Unclassified,
            // Always sessions; IsCardioSession answered above.
            WorkoutCategory.Endurance or WorkoutCategory.Sport => throw new System.Diagnostics.UnreachableException(),
            _ => throw new System.Diagnostics.UnreachableException(),
        });
    }

    private static double? DistanceMeters(CompletedTrainingSession session)
    {
        var distances = session.Sets
            .Select(set => set switch
            {
                TrailRunSet run => run.Distance * run.Unit.MetersEach(),
                EnduranceSet endurance => endurance.Distance * endurance.Unit.MetersEach(),
                DistanceTimeSet timed => timed.Distance * timed.Unit.MetersEach(),
                _ => (double?)null,
            })
            .OfType<double>()
            .ToList();

        return distances.Count is 0 ? null : distances.Sum();
    }

    // ---- Heart rate across a session ----

    /// <summary>
    /// The samples a session's heart rate is read from: the recording app's, for a run imported
    /// from Apple Health with any, and otherwise the ring's series over its clock time.
    /// </summary>
    private static (IReadOnlyList<HeartRateSample> Samples, HeartRateSource Source) HeartRateRead(
        CardioInputs inputs, GatheredSession session) =>
        session.Source is CardioSessionSource.LoggedSession logged
        && inputs.RecordedHeartRate.TryGetValue(logged.TrainingSessionId, out var recorded)
        && recorded.IsRecorded
            ? ([.. recorded.Samples.Where(sample => sample.At >= session.Start && sample.At < session.End)], HeartRateSource.AppleHealth)
            : (Samples(inputs.HeartRate, session), HeartRateSource.Ring);

    /// <summary>
    /// The samples inside a session, from a run sorted by instant: found by bisection rather than
    /// a scan, since a year's report cuts a few hundred sessions out of a hundred thousand samples.
    /// </summary>
    private static IReadOnlyList<HeartRateSample> Samples(IReadOnlyList<HeartRateSample> sorted, GatheredSession session)
    {
        var first = FirstAtOrAfter(sorted, session.Start);
        var end = FirstAtOrAfter(sorted, session.End);
        return [.. Enumerable.Range(first, end - first).Select(index => sorted[index])];
    }

    private static int FirstAtOrAfter(IReadOnlyList<HeartRateSample> sorted, DateTimeOffset instant)
    {
        var (low, high) = (0, sorted.Count);
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (sorted[middle].At < instant) low = middle + 1;
            else high = middle;
        }
        return low;
    }

    /// <summary>The recorded peak across the cardio sessions of the year ending on <paramref name="lastDay"/>.</summary>
    private static int? Peak(
        IEnumerable<(GatheredSession Session, IReadOnlyList<HeartRateSample> Samples)> sessions, DateOnly lastDay)
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

    /// <summary>What a session's samples say about it: average and peak, coverage, and zones.</summary>
    public static SessionHeartRate HeartRateOf(
        GatheredSession session, IReadOnlyList<HeartRateSample> samples, HeartRateSource source, MaxHeartRate? maximum)
    {
        if (samples.Count is 0) return new SessionHeartRate.NotRecorded();

        var attributed = SampleTime.Attributed(samples, session.End).ToList();
        var minutes = attributed.Sum(pair => pair.Minutes);
        var sessionMinutes = (session.End - session.Start).TotalMinutes;

        return new SessionHeartRate.Recorded(
            (int)Math.Round(SampleTime.AverageBpm(attributed)),
            samples.Max(sample => sample.Bpm),
            sessionMinutes > 0 ? Math.Min(1, minutes / sessionMinutes) : 0,
            maximum is null
                ? null
                : attributed.Aggregate(
                    ZoneMinutes.None,
                    (zones, pair) => zones + ZoneMinutes.InZone(ZoneMinutes.ZoneOf(pair.Sample.Bpm, maximum.Bpm), pair.Minutes)),
            source);
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
                    Enumerable.Range(0, 7).Count(offset => Within(range, week.AddDays(offset))),
                    byWeek[week].Count(),
                    byWeek[week].Sum(session => session.Minutes),
                    byWeek[week].Aggregate(ZoneMinutes.None, (zones, session) => zones + ZonesOf(session))))
        ];
    }

    public static DateOnly WeekStarting(DateOnly day) =>
        day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    private static AerobicBase AerobicBaseOf(
        CardioInputs inputs, IReadOnlyList<CardioSession> sessions, MaxHeartRate? maximum)
    {
        var recent = new DayRange(inputs.Range.To.AddDays(1 - 7 * AerobicBase.RecentWeeks), inputs.Range.To);
        var before = new DayRange(
            recent.From.AddDays(-7 * AerobicBase.BeforeWeeks), recent.From.AddDays(-1));

        Comparison Compare(Func<DayRange, double?> figure) => new(figure(recent), figure(before));

        IReadOnlyList<CardioSession> In(DayRange window) =>
            [.. sessions.Where(session => Within(window, LocalDay(session.Start)))];

        double PerWeek(DayRange window, Func<CardioSession, double> minutes) =>
            In(window).Sum(minutes) / (window.Days / 7.0);

        // Unknown rather than low when there is no maximum to draw zones from, or when most of the
        // window's session time has no heart rate synced behind it. A window with no sessions at
        // all has a real zero.
        double? Zone2(DayRange window)
        {
            if (maximum is null) return null;

            var held = In(window);
            var total = held.Sum(session => session.Minutes);
            var covered = held
                .Where(session => session.HeartRate is SessionHeartRate.Recorded)
                .Sum(session => session.Minutes);

            return total > 0 && covered < ZoneCoverageFloor * total
                ? null
                : PerWeek(window, session => ZonesOf(session).Zone2);
        }

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
            Compare(window => Mean(window, inputs.CardiovascularAge.Select(age => (age.Day, (double?)age.Reading.VascularAge)))))
        {
            AppleWatchVo2Max = Compare(window => Mean(window, inputs.AppleWatchVo2Max.Select(estimate => (estimate.Day, (double?)estimate.Value)))),
        };
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
            .Where(value => Within(range, value.Day))
            .Select(value => value.Value is { } held ? new DayValue(value.Day, held) : null)
            .OfType<DayValue>()
            .GroupBy(value => value.Day)
            .OrderBy(day => day.Key)
            .Select(day => day.First())
    ];

    /// <summary>The day a session began on the athlete's own clock, at the offset it was recorded with.</summary>
    private static DateOnly LocalDay(DateTimeOffset instant) => DateOnly.FromDateTime(instant.DateTime);

    private static bool Within(DayRange range, DateOnly day) => day >= range.From && day <= range.To;
}
