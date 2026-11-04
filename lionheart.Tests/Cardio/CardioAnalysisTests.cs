using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Model.Health;
using lionheart.Model.Oura;
using lionheart.Model.Oura.Readings;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services.Cardio;
using lionheart.Tests.Support;

namespace lionheart.Tests.Cardio;

/// <summary>
/// The cardio report's rules, over hand-built inputs: which workouts are sessions, how heart-rate
/// time is attributed, which maximum the zones come from, and what the comparison says when the
/// series is missing.
/// </summary>
public class CardioAnalysisTests
{
    private static readonly Guid Athlete = Guid.NewGuid();
    private static readonly TimeSpan Eastern = TimeSpan.FromHours(-4);
    private static readonly DayRange October = new(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

    // ---- Which workouts are sessions ----

    [Fact]
    public void A_walk_under_half_an_hour_is_excluded_and_a_longer_one_is_a_session()
    {
        var report = Build(ring: [Ring("walking", At(10, 9), 19), Ring("walking", At(11, 9), 45)]);

        var session = Assert.Single(report.Sessions);
        Assert.Equal(45, session.Minutes, precision: 3);
        var excluded = Assert.Single(report.Excluded);
        Assert.Equal((WorkoutCategory.Walk, 1), (excluded.Category, excluded.Count));
        Assert.Equal(WorkoutExclusion.ShortWalk, excluded.Reason);
    }

    [Fact]
    public void Housework_and_stretching_are_counted_as_excluded_never_as_cardio()
    {
        var report = Build(ring: [Ring("houseWork", At(10, 9), 40), Ring("stretching", At(10, 12), 20)]);

        Assert.Empty(report.Sessions);
        Assert.Equal(2, Assert.Single(report.Excluded).Count);
    }

    [Fact]
    public void A_ring_workout_inside_a_logged_lifting_session_is_that_session_not_cardio()
    {
        // Most of the ring's "handball" is the gym: a lifting session the ring saw and misnamed.
        var lifting = Logged(At(12, 17), 70, Some.LiftSet);
        var report = Build(ring: [Ring("handball", At(12, 17).AddMinutes(10), 30)], logged: [lifting]);

        Assert.Empty(report.Sessions);
        Assert.Equal(WorkoutExclusion.InsideLoggedLifting, Assert.Single(report.Excluded).Reason);
    }

    [Fact]
    public void A_logged_run_the_ring_also_saw_is_one_session_kept_as_the_logged_one()
    {
        var run = Logged(At(14, 7), 60, Some.TrailRunSet);
        var report = Build(ring: [Ring("running", At(14, 7).AddMinutes(4), 52)], logged: [run]);

        var session = Assert.Single(report.Sessions);
        var source = Assert.IsType<CardioSessionSource.LoggedSession>(session.Source);
        Assert.True(source.IsTrailRun);
        Assert.Equal(6.2 * 1609.344, session.DistanceMeters!.Value, precision: 3);
    }

    [Fact]
    public void A_ring_workout_inside_a_run_imported_from_Apple_Health_is_that_run()
    {
        var run = Logged(At(14, 7), 60, Some.TrailRunSet) with { Source = AHealthRun.Source() };
        var report = Build(ring: [Ring("running", At(14, 7).AddMinutes(4), 52)], logged: [run]);

        var source = Assert.IsType<CardioSessionSource.LoggedSession>(Assert.Single(report.Sessions).Source);
        Assert.True(source.IsRecorded);
        Assert.Equal(WorkoutExclusion.SameAsLoggedRun, Assert.Single(report.Excluded).Reason);
    }

    // ---- Which of the ring's guesses were training ----
    // Age 20 predicts a maximum of 194: zone 2 from 117 bpm, zone 3 from 136.

    [Fact]
    public void A_detected_game_of_catch_in_zone_one_is_declined_and_shown_with_its_heart_rate()
    {
        var start = At(10, 17);
        var report = Build(ring: [Detected("handball", start, 25)], heartRate: Minutes(start, (105, 25)));

        Assert.Empty(report.Sessions);
        Assert.Equal(0, report.Weeks.Sum(week => week.Zones.Zone2));
        var declined = Assert.Single(report.Declined);
        Assert.Equal(WorkoutExclusion.HeartRateBelowTraining, declined.Reason);
        Assert.Equal(105, Assert.IsType<SessionHeartRate.Recorded>(declined.Session.HeartRate).AverageBpm);
        var excluded = Assert.Single(report.Excluded);
        Assert.Equal((WorkoutCategory.Sport, WorkoutExclusion.HeartRateBelowTraining, 1), (excluded.Category, excluded.Reason, excluded.Count));
    }

    [Fact]
    public void A_detected_game_held_in_zone_two_counts_and_its_minutes_with_it()
    {
        var start = At(10, 17);
        var report = Build(ring: [Detected("handball", start, 40)], heartRate: Minutes(start, (125, 30), (100, 10)));

        var source = Assert.IsType<CardioSessionSource.RingWorkout>(Assert.Single(report.Sessions).Source);
        Assert.Equal(RingEntry.Detected, source.Entry);
        Assert.Equal(30, report.Weeks.Sum(week => week.Zones.Zone2), precision: 3);
        Assert.Empty(report.Declined);
    }

    [Fact]
    public void A_detected_workout_with_no_heart_rate_is_declined_as_unread_rather_than_counted()
    {
        var report = Build(ring: [Detected("basketball", At(12, 17), 45)]);

        Assert.Empty(report.Sessions);
        Assert.Equal(WorkoutExclusion.HeartRateUnread, Assert.Single(report.Declined).Reason);
        // Declined, but still session time with nothing behind it: the window's zone 2 is unknown, not zero.
        Assert.Null(report.AerobicBase.Zone2MinutesPerWeek.Recent);
    }

    [Fact]
    public void A_declined_guess_still_tells_the_maximum_what_rate_was_reached()
    {
        // 199 bpm is above the 194 predicted for age 20; the spike inside an hour of pottering is
        // declined, but a rate reached is evidence about the maximum whatever the activity was.
        var start = At(10, 17);
        var report = Build(
            ring: [Detected("handball", start, 600)],
            heartRate: [.. Minutes(start, (95, 480)), .. Minutes(start.AddMinutes(480), (199, 120))]);

        Assert.Equal(WorkoutExclusion.HeartRateBelowTraining, Assert.Single(report.Declined).Reason);
        Assert.Equal(199, report.MaxHeartRate?.Bpm);
    }

    [Fact]
    public void A_workout_started_on_the_ring_is_the_athletes_own()
    {
        var report = Build(ring: [Ring("basketball", At(12, 17), 45, source: "workout_heart_rate")]);

        Assert.Equal(RingEntry.Entered, Assert.IsType<CardioSessionSource.RingWorkout>(Assert.Single(report.Sessions).Source).Entry);
    }

    [Fact]
    public void A_workout_the_athlete_entered_is_taken_at_its_name_without_heart_rate()
    {
        var report = Build(ring: [Ring("basketball", At(12, 17), 45)]);

        Assert.Equal(RingEntry.Entered, Assert.IsType<CardioSessionSource.RingWorkout>(Assert.Single(report.Sessions).Source).Entry);
        Assert.Empty(report.Declined);
    }

    [Fact]
    public void Housework_never_counts_whatever_its_heart_rate()
    {
        var start = At(10, 9);
        var report = Build(ring: [Detected("houseWork", start, 40)], heartRate: Minutes(start, (130, 40)));

        Assert.Empty(report.Sessions);
        Assert.Empty(report.Declined);
        Assert.Equal(WorkoutExclusion.NotTraining, Assert.Single(report.Excluded).Reason);
    }

    [Fact]
    public void An_entered_workout_nobody_named_is_still_judged_by_its_heart_rate()
    {
        var start = At(10, 9);
        var report = Build(ring: [Ring("other", start, 30)], heartRate: Minutes(start, (100, 30)));

        Assert.Empty(report.Sessions);
        Assert.Equal(WorkoutCategory.Unspecified, Assert.Single(report.Excluded).Category);
    }

    [Theory]
    [InlineData(0, 25, 0, 0, TrainingVerdict.BelowTraining)] // catch: all zone 1
    [InlineData(10, 10, 20, 0, TrainingVerdict.Training)] // twenty minutes at zone 2
    [InlineData(0, 3, 0, 12, TrainingVerdict.Training)] // a short hard effort
    [InlineData(0, 0, 0, 9, TrainingVerdict.BelowTraining)] // too short to be a hard session
    [InlineData(10, 30, 20, 0, TrainingVerdict.BelowTraining)] // a spike inside an hour of pottering
    [InlineData(0, 20, 20, 0, TrainingVerdict.Training)] // exactly half the time at zone 2 and above
    public void The_heart_rate_shows_training_by_time_at_zone_two_or_above_and_its_share(
        double belowZones, double zone1, double zone2, double zone3, TrainingVerdict verdict)
    {
        var zones = new ZoneMinutes(belowZones, zone1, zone2, zone3, 0, 0);

        Assert.Equal(verdict, TrainingEvidence.Judge(new SessionHeartRate.Recorded(120, 150, 1, zones, HeartRateSource.Ring)));
    }

    [Fact]
    public void Heart_rate_over_too_little_of_a_workout_cannot_show_training()
    {
        var zones = new ZoneMinutes(0, 0, 30, 0, 0, 0);

        Assert.Equal(TrainingVerdict.Unread, TrainingEvidence.Judge(new SessionHeartRate.Recorded(125, 130, 0.4, zones, HeartRateSource.Ring)));
        Assert.Equal(TrainingVerdict.Unread, TrainingEvidence.Judge(new SessionHeartRate.Recorded(125, 130, 1, null, HeartRateSource.Ring)));
    }

    // ---- Heart rate across a session ----

    [Fact]
    public void An_imported_run_reads_the_recording_apps_heart_rate_ahead_of_the_rings_and_says_whose()
    {
        var start = At(16, 7);
        var run = Logged(start, 30, Some.TrailRunSet) with { Source = AHealthRun.Source() };
        var strap = RecordedHeartRate.Of(Enumerable.Range(0, 30).Select(minute => (start.AddMinutes(minute), 160)));

        var report = Build(
            logged: [run],
            heartRate: Beats(start, 120, count: 6),
            recorded: new Dictionary<Guid, RecordedHeartRate> { [run.TrainingSessionId] = strap });

        var heartRate = Assert.IsType<SessionHeartRate.Recorded>(Assert.Single(report.Sessions).HeartRate);
        Assert.Equal((160, HeartRateSource.AppleHealth), (heartRate.AverageBpm, heartRate.Source));
    }

    [Fact]
    public void An_imported_run_whose_app_wrote_no_heart_rate_reads_the_rings_and_says_so()
    {
        var start = At(16, 7);
        var run = Logged(start, 30, Some.TrailRunSet) with { Source = AHealthRun.Source() };

        var report = Build(
            logged: [run],
            heartRate: Beats(start, 120, count: 6),
            recorded: new Dictionary<Guid, RecordedHeartRate> { [run.TrainingSessionId] = RecordedHeartRate.None });

        var heartRate = Assert.IsType<SessionHeartRate.Recorded>(Assert.Single(report.Sessions).HeartRate);
        Assert.Equal((120, HeartRateSource.Ring), (heartRate.AverageBpm, heartRate.Source));
    }

    [Fact]
    public void Each_sample_stands_for_the_time_to_the_next_and_the_zones_follow_the_maximum()
    {
        // Age 20 predicts 194. 120 bpm is 62% of it (zone 2), 150 is 77% (zone 3).
        var start = At(15, 7);
        var report = Build(
            ring: [Ring("running", start, 30)],
            heartRate: [.. Beats(start, 120, count: 3), .. Beats(start.AddMinutes(15), 150, count: 3)]);

        var recorded = Assert.IsType<SessionHeartRate.Recorded>(Assert.Single(report.Sessions).HeartRate);
        Assert.Equal(new ZoneMinutes(0, 0, 15, 15, 0, 0), recorded.Zones);
        Assert.Equal(135, recorded.AverageBpm);
        Assert.Equal(150, recorded.PeakBpm);
        Assert.Equal(1, recorded.Coverage, precision: 6);
    }

    [Fact]
    public void A_gap_in_the_series_is_time_without_a_reading_rather_than_time_at_the_last_one()
    {
        var start = At(15, 7);
        var report = Build(
            ring: [Ring("running", start, 60)],
            heartRate: [Beat(start, 130), Beat(start.AddMinutes(50), 130)]);

        var recorded = Assert.IsType<SessionHeartRate.Recorded>(Assert.Single(report.Sessions).HeartRate);
        Assert.Equal(10, recorded.Zones!.InZones, precision: 6);
        Assert.Equal(10.0 / 60, recorded.Coverage, precision: 6);
    }

    [Fact]
    public void A_session_with_no_samples_says_its_heart_rate_was_not_recorded()
    {
        var report = Build(ring: [Ring("hiking", At(16, 9), 90)]);

        Assert.IsType<SessionHeartRate.NotRecorded>(Assert.Single(report.Sessions).HeartRate);
    }

    // ---- The maximum ----

    [Fact]
    public void The_age_prediction_sets_the_zones_when_the_rings_peak_is_below_it()
    {
        var start = At(15, 7);
        var report = Build(ring: [Ring("running", start, 600)], heartRate: Beats(start, 170, count: 120));

        var maximum = Assert.IsType<MaxHeartRate.AgePredicted>(report.MaxHeartRate);
        Assert.Equal((194, 20, 170), (maximum.Bpm, maximum.Age, maximum.ObservedPeak));
    }

    [Fact]
    public void A_ring_peak_above_the_age_prediction_is_taken_as_the_maximum()
    {
        var start = At(15, 7);
        var report = Build(ring: [Ring("running", start, 600)], heartRate: Beats(start, 199, count: 120));

        var maximum = Assert.IsType<MaxHeartRate.Observed>(report.MaxHeartRate);
        Assert.Equal((199, 194), (maximum.Bpm, maximum.AgePredictedBpm));
    }

    [Fact]
    public void Too_few_cardio_samples_report_no_peak_at_all()
    {
        var start = At(15, 7);
        var report = Build(ring: [Ring("running", start, 60)], heartRate: Beats(start, 199, count: 12));

        var maximum = Assert.IsType<MaxHeartRate.AgePredicted>(report.MaxHeartRate);
        Assert.Null(maximum.ObservedPeak);
    }

    [Fact]
    public void A_profile_with_no_usable_age_and_no_peak_draws_no_zones()
    {
        var start = At(15, 7);
        var report = Build(ring: [Ring("running", start, 30)], heartRate: Beats(start, 140, count: 6), age: 0);

        Assert.Null(report.MaxHeartRate);
        Assert.Null(Assert.IsType<SessionHeartRate.Recorded>(Assert.Single(report.Sessions).HeartRate).Zones);
    }

    // ---- Weeks and the comparison ----

    [Fact]
    public void Every_week_the_range_touches_is_present_including_the_empty_ones()
    {
        var report = Build(ring: [Ring("running", At(14, 7), 40)]);

        // 1 October 2026 is a Thursday, so the first week starts on Monday 28 September.
        Assert.Equal(new DateOnly(2026, 9, 28), report.Weeks[0].WeekStarting);
        Assert.Equal(new DateOnly(2026, 10, 26), report.Weeks[^1].WeekStarting);
        Assert.Equal([0, 0, 1, 0, 0], report.Weeks.Select(week => week.Sessions));
    }

    [Fact]
    public void Zone_two_is_unknown_rather_than_zero_when_the_window_has_sessions_but_no_heart_rate()
    {
        var report = Build(ring: [Ring("running", At(20, 7), 40)]);

        Assert.Null(report.AerobicBase.Zone2MinutesPerWeek.Recent);
        Assert.Equal(10, report.AerobicBase.CardioMinutesPerWeek.Recent!.Value, precision: 6);
    }

    [Fact]
    public void Zone_two_is_unknown_when_most_of_a_windows_session_time_has_no_heart_rate()
    {
        // One 30-minute session with heart rate and a 90-minute one before the series was synced:
        // counting only the first would read as a quarter of the zone 2 that happened.
        var start = At(20, 7);
        var report = Build(
            ring: [Ring("running", start, 30), Ring("hiking", At(21, 9), 90)],
            heartRate: Beats(start, 125, count: 6));

        Assert.Null(report.AerobicBase.Zone2MinutesPerWeek.Recent);
    }

    [Fact]
    public void Zone_two_is_unknown_rather_than_zero_with_no_maximum_to_draw_zones_from()
    {
        var start = At(20, 7);
        var report = Build(ring: [Ring("running", start, 30)], heartRate: Beats(start, 125, count: 6), age: 0);

        Assert.Null(report.AerobicBase.Zone2MinutesPerWeek.Recent);
    }

    [Fact]
    public void A_week_cut_by_the_range_says_how_many_of_its_days_are_in_it()
    {
        var report = Build();

        // October 2026 starts on a Thursday and ends on a Saturday.
        Assert.Equal([4, 7, 7, 7, 6], report.Weeks.Select(week => week.DaysInRange));
    }

    [Fact]
    public void Zone_two_is_zero_in_a_window_with_no_cardio_at_all()
    {
        var report = Build();

        Assert.Equal(0, report.AerobicBase.Zone2MinutesPerWeek.Recent);
    }

    [Fact]
    public void Resting_heart_rate_reads_a_lowest_rate_of_zero_as_no_reading()
    {
        var report = Build(sleeps: [Night(new DateOnly(2026, 10, 20), 44), Night(new DateOnly(2026, 10, 21), 0)]);

        Assert.Equal([new DayValue(new DateOnly(2026, 10, 20), 44)], report.RestingHeartRate);
    }

    // ---- Builders ----

    private static CardioReport Build(
        IReadOnlyList<OuraWorkout>? ring = null,
        IReadOnlyList<CompletedTrainingSession>? logged = null,
        IReadOnlyList<HeartRateSample>? heartRate = null,
        IReadOnlyList<OuraSleepPeriod>? sleeps = null,
        IReadOnlyDictionary<Guid, RecordedHeartRate>? recorded = null,
        int age = 20) =>
        CardioAnalysis.Build(new CardioInputs(
            October, age, CardioAnalysis.Gather(ring ?? [], logged ?? []),
            [.. (heartRate ?? []).OrderBy(sample => sample.At)],
            recorded ?? new Dictionary<Guid, RecordedHeartRate>(),
            [], [], sleeps ?? []));

    private static DateTimeOffset At(int day, int hour) => new(2026, 10, day, hour, 0, 0, Eastern);

    /// <summary>A workout on the ring: entered by the athlete unless <paramref name="source"/> says the ring detected it.</summary>
    private static OuraWorkout Ring(string activity, DateTimeOffset start, double minutes, string source = "manual") =>
        new(Guid.NewGuid(), Athlete, DateOnly.FromDateTime(start.DateTime), new WorkoutReading(
            new Interval(start, start.AddMinutes(minutes)), activity, "moderate", source, null, null, null));

    private static OuraWorkout Detected(string activity, DateTimeOffset start, double minutes) =>
        Ring(activity, start, minutes, "confirmed");

    /// <summary>One sample a minute from <paramref name="start"/>, at each rate in turn for its minutes.</summary>
    private static IReadOnlyList<HeartRateSample> Minutes(DateTimeOffset start, params (int Bpm, int Minutes)[] stretches) =>
    [
        .. stretches
            .SelectMany(stretch => Enumerable.Repeat(stretch.Bpm, stretch.Minutes))
            .Select((bpm, minute) => Beat(start.AddMinutes(minute), bpm))
    ];

    private static CompletedTrainingSession Logged(DateTimeOffset start, double minutes, SetEntry set) =>
        Some.CompletedSession(Athlete) with
        {
            Sets = [set],
            Period = new Interval(start, start.AddMinutes(minutes)),
        };

    private static HeartRateSample Beat(DateTimeOffset at, int bpm) => new(at.ToUniversalTime(), bpm, "workout");

    /// <summary><paramref name="count"/> samples five minutes apart from <paramref name="start"/>.</summary>
    private static IReadOnlyList<HeartRateSample> Beats(DateTimeOffset start, int bpm, int count) =>
        [.. Enumerable.Range(0, count).Select(index => Beat(start.AddMinutes(5 * index), bpm))];

    private static OuraSleepPeriod Night(DateOnly day, int lowest)
    {
        var bedtime = new DateTimeOffset(day.AddDays(-1).ToDateTime(new TimeOnly(23, 0)), Eastern);
        var empty = new OuraSampleSeries(300, [], bedtime);
        return new OuraSleepPeriod(Guid.NewGuid(), Athlete, day, new SleepPeriodReading(
            new Interval(bedtime, bedtime.AddHours(8)), SleepPeriodReading.MainSleepKind, 0, 28800, 25200,
            5400, 14400, 5400, 3600, 600, 88, 10, 50, lowest, 80, 14, null, null, null, null, "4222", empty, empty));
    }
}
