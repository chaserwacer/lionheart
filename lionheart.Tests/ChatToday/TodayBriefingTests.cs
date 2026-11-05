using lionheart.Model.Cardio;
using lionheart.Model.Training.Patterns;
using lionheart.Services.Chat.Today;

namespace lionheart.Tests.ChatToday;

/// <summary>
/// What the briefing says, from facts built by hand. The claims that matter are the ones whose
/// absence caused the wrong answers it exists to prevent: which day is today on the athlete's own
/// clock, what he trained on which day, and that a missing reading is said to be missing.
/// </summary>
public sealed class TodayBriefingTests
{
    private static readonly TimeSpan Eastern = TimeSpan.FromHours(-5);

    /// <summary>An evening on his clock that is already the next day in UTC, where a UTC day boundary names the wrong day.</summary>
    private static readonly AthleteNow Evening = new(new DateTimeOffset(2026, 3, 10, 21, 40, 0, Eastern));

    private static TodayFacts Facts(
        AthleteNow? now = null,
        IReadOnlyList<SessionFact>? sessions = null,
        IReadOnlyList<CardioFact>? cardio = null,
        IReadOnlyList<NightFact>? nights = null,
        ReadinessFact? readiness = null,
        IReadOnlyList<InjuryFact>? injuries = null,
        MaxHeartRate? max = null,
        string score = "",
        IReadOnlyList<NonTrainingFact>? nonTraining = null) =>
        new(now ?? Evening,
            sessions ?? [],
            new CardioDays.Read(cardio ?? [], max),
            new LoadFact(20, 16, 3, 2.5),
            nights ?? [],
            new NightBaseline(70, 45, 40),
            readiness,
            new WeekMeans(null, null, 0),
            null,
            injuries ?? [],
            [],
            nonTraining ?? []) { Score = score };

    private static SessionFact Lifting(DateOnly day, int hour, params (string Movement, PatternFamily Family, int Sets)[] lifts) =>
        new(day,
            new DateTimeOffset(day.ToDateTime(new TimeOnly(hour, 0)), Eastern),
            TimeSpan.FromMinutes(50),
            [.. lifts.Select(lift => new LiftFact(lift.Movement, lift.Family, lift.Sets, "100 lb × 5"))],
            HasCardioSets: false,
            Difficulty: 3,
            Fatigue: null);

    [Fact]
    public void The_days_marked_non_training_ahead_are_stated_with_his_notes()
    {
        var briefing = TodayBriefing.Render(Facts(nonTraining:
        [
            new NonTrainingFact(Evening.Today, "travel"),
            new NonTrainingFact(Evening.Today.AddDays(5), ""),
        ]));

        Assert.Contains("Marked non-training (he has said he will not train): today (travel), Sun 15 Mar.", briefing);
    }

    [Fact]
    public void Now_is_stated_on_the_athletes_clock_with_its_offset_not_in_utc()
    {
        var briefing = TodayBriefing.Render(Facts());

        Assert.Contains("Now: Tuesday 10 March 2026, 21:40 (UTC-05:00).", briefing);
        Assert.Contains("This week runs Monday 9 March to Sunday 15 March.", briefing);
        Assert.DoesNotContain("11 March", briefing);
    }

    [Fact]
    public void Training_is_placed_on_the_day_it_happened_and_yesterday_is_not_today()
    {
        var briefing = TodayBriefing.Render(Facts(sessions:
        [
            Lifting(new DateOnly(2026, 3, 9), 17, ("Paused Barbell Squat", PatternFamily.Legs, 4)),
        ]));

        Assert.Contains("Trained today: nothing logged yet.", briefing);
        Assert.Contains("Yesterday (Monday 9 March): lifting at 17:00, 50 min, 4 hard sets (legs 4): Paused Barbell Squat 4 sets (top 100 lb × 5)", briefing);
    }

    [Fact]
    public void A_session_today_is_stated_by_family_so_upper_or_legs_is_never_inferred()
    {
        var briefing = TodayBriefing.Render(Facts(sessions:
        [
            Lifting(new DateOnly(2026, 3, 10), 18, ("Bench Press", PatternFamily.Push, 3), ("Cable Row", PatternFamily.Pull, 3)),
        ]));

        Assert.Contains("Trained today: lifting at 18:00, 50 min, 6 hard sets (push 3, pull 3)", briefing);
        Assert.Contains("he rated it difficulty 3/5", briefing);
    }

    [Fact]
    public void Cardio_the_report_counted_is_listed_on_its_day_with_its_zone_minutes()
    {
        var briefing = TodayBriefing.Render(Facts(cardio:
        [
            new CardioFact(new DateOnly(2026, 3, 10), new DateTimeOffset(2026, 3, 10, 7, 5, 0, Eastern), 42, "cycling", 128, 35, 0),
        ]));

        Assert.Contains("Trained today: cycling at 07:05, 42 min, avg 128 bpm, zone 2 35 min, zone 4-5 0 min.", briefing);
    }

    [Fact]
    public void A_night_or_a_readiness_not_yet_synced_is_said_to_be_missing()
    {
        var briefing = TodayBriefing.Render(Facts());

        Assert.Contains("Sleep: last night not synced yet; the night before not synced yet.", briefing);
        Assert.Contains("Readiness today: not synced yet.", briefing);
    }

    [Fact]
    public void Last_night_is_the_night_filed_under_today()
    {
        var night = new NightFact(
            new DateOnly(2026, 3, 10),
            new DateTimeOffset(2026, 3, 9, 23, 30, 0, Eastern),
            new DateTimeOffset(2026, 3, 10, 7, 0, 0, Eastern),
            TotalSleepSeconds: 7 * 3600 + 5 * 60, Efficiency: 90, LowestHeartRate: 44, AverageHrv: 71, SleepScore: 82);

        var briefing = TodayBriefing.Render(Facts(nights: [night]));

        Assert.Contains("Sleep: last night 23:30-07:00, 7h05m asleep, efficiency 90%, lowest heart rate 44 bpm, HRV 71 ms, sleep score 82;", briefing);
        Assert.Contains("Baseline over the 40 nights before: HRV 70 ms, lowest heart rate 45 bpm.", briefing);
    }

    [Fact]
    public void Time_asleep_rounds_to_the_minute_as_the_app_reads_it()
    {
        var night = new NightFact(
            new DateOnly(2026, 3, 10),
            new DateTimeOffset(2026, 3, 9, 23, 30, 0, Eastern),
            new DateTimeOffset(2026, 3, 10, 7, 0, 0, Eastern),
            TotalSleepSeconds: 6 * 3600 + 41 * 60 + 40, Efficiency: 90, LowestHeartRate: 44, AverageHrv: 71, SleepScore: 82);

        var briefing = TodayBriefing.Render(Facts(nights: [night]));

        Assert.Contains("6h42m asleep", briefing);
    }

    [Fact]
    public void Readiness_is_stated_against_his_usual_range_as_scores_not_measurements()
    {
        var briefing = TodayBriefing.Render(Facts(readiness: new ReadinessFact(68, 74, 80, -0.2, 66, 88)));

        Assert.Contains("Readiness today (Oura's 1-100 scores, not measurements): 68/100 (usual 66-88 over 90 days), HRV balance 74/100, resting heart rate score 80/100, temperature -0.4 °F.", briefing);
    }

    [Fact]
    public void Active_injuries_are_named_with_their_last_pain_and_none_is_said_outright()
    {
        Assert.Contains("Active injuries: none.", TodayBriefing.Render(Facts()));
        Assert.Contains(
            "Active injuries: Left knee (last pain 3/10).",
            TodayBriefing.Render(Facts(injuries: [new InjuryFact("Left knee", 3)])));
    }

    [Fact]
    public void Zones_are_given_in_bpm_from_the_same_maximum_the_cardio_report_uses()
    {
        var briefing = TodayBriefing.Render(Facts(max: new MaxHeartRate.AgePredicted(190, 25, ObservedPeak: 172)));

        Assert.Contains("Heart-rate zones (maximum 190 bpm, age-predicted; the ring's cardio samples over the last year reach 172 at the 99th percentile): zone 2 114-132, zone 3 133-151, zone 4 152-170, zone 5 171+ bpm.", briefing);
    }

    [Fact]
    public void The_score_is_passed_through_as_the_tool_states_it()
    {
        var briefing = TodayBriefing.Render(Facts(score: """{"score":"64 of 100"}"""));

        Assert.EndsWith("""{"score":"64 of 100"}""", briefing);
    }

    [Fact]
    public void The_week_lists_every_day_from_monday_including_the_empty_ones()
    {
        var briefing = TodayBriefing.Render(Facts(sessions:
        [
            Lifting(new DateOnly(2026, 3, 9), 17, ("Paused Barbell Squat", PatternFamily.Legs, 4)),
        ]));

        Assert.Contains("This week so far: Mon lifting at 17:00, 50 min, 4 hard sets (legs 4) (Paused Barbell Squat); he rated it difficulty 3/5; Tue nothing.", briefing);
    }

    [Fact]
    public void When_nothing_else_can_be_read_the_date_is_still_stated()
    {
        var briefing = TodayBriefing.ClockOnly(Evening);

        Assert.Contains("Now: Tuesday 10 March 2026, 21:40 (UTC-05:00).", briefing);
        Assert.Contains("do not assume what he did today", briefing);
    }

    [Fact]
    public void Every_briefing_tells_the_model_to_state_figures_in_imperial_units()
    {
        // The tools hand over meters, kilograms and °C; he reads none of them.
        Assert.Contains(TodayBriefing.Units, TodayBriefing.Render(Facts()));
        Assert.Contains(TodayBriefing.Units, TodayBriefing.ClockOnly(Evening));
    }

    // ---- The clock ----

    [Fact]
    public void Today_is_the_athletes_day_even_after_utc_midnight()
    {
        Assert.Equal(new DateOnly(2026, 3, 10), Evening.Today);
        Assert.Equal(new DateOnly(2026, 3, 9), Evening.Yesterday);
    }

    [Theory]
    [InlineData(9, 9)]   // Monday
    [InlineData(15, 9)]  // Sunday belongs to the week that began the Monday before
    [InlineData(16, 16)] // the next Monday starts a new week
    public void The_week_runs_monday_to_sunday(int day, int monday) =>
        Assert.Equal(new DateOnly(2026, 3, monday), new AthleteNow(new DateTimeOffset(2026, 3, day, 12, 0, 0, Eastern)).WeekStart);
}
