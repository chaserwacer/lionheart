using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Model.Training.Expectations;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.GoingIn;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Sets;
using lionheart.Tests.Support;
using static lionheart.Tests.Balance.ALog;

namespace lionheart.Tests.Balance;

/// <summary>
/// A priority lift's next top set: one step on last time's best set, held when the going-in rating,
/// the last top set's RPE, the last session's difficulty or the week's fatigue says so, with a pinned
/// target's distance and frequency beside it.
/// </summary>
public class LiftAimAnalysisTests
{
    private static readonly DateOnly Today = new(2026, 11, 4);

    private static readonly Func<DayRange, CardioMeasure> NoHeartRate =
        _ => new CardioMeasure.Unknown(CardioUnknownReason.NoHeartRate);

    private static PriorityAim PushAim(
        ALog log, GoingInRating? goingIn = null, IReadOnlyList<FocusPin>? pins = null, Model.Training.Availability.AvailabilityCalendar? days = null) =>
        Scoring.Score(
                log.Build(), pins ?? [], NoHeartRate, Today, days,
                goingIn is null ? null : new Dictionary<DateOnly, GoingInRating> { [Today] = goingIn })
            .Expectations.Priorities.Single(priority => priority.Family == PatternFamily.Push);

    /// <summary>Bench last trained three days ago, its top set 5 × 200 lb.</summary>
    private static ALog BenchedThreeDaysAgo(Rpe? rpe = null, int? fatigue = null, int? difficulty = null) =>
        Catalogue.WithRatedSession(Today.AddDays(-3), fatigue, difficulty, Set(Bench, 200, 5) with { Rpe = rpe ?? Rpe.Unrated });

    private static FocusPin BenchPinned(FocusTarget target, int sessionsPerWeek = 2) =>
        FocusPin.Pin(
            Guid.NewGuid(), Guid.NewGuid(), Bench,
            ResultAssert.Succeeded(FocusRole.Priority.For(PatternFamily.Push)),
            ResultAssert.Succeeded(FocusTerms.Of(
                ResultAssert.Succeeded(FocusHorizon.Of(Today.AddDays(-30), Today.AddDays(30))), sessionsPerWeek, target)),
            DateTime.UnixEpoch);

    [Fact]
    public void An_easy_last_top_set_steps_five_pounds_and_says_why()
    {
        var aim = PushAim(BenchedThreeDaysAgo(rpe: Rpe.Of(7.5)));

        var beat = Assert.IsType<LiftAim.Beat>(aim.Aim);
        Assert.Equal((205d, 5), (beat.Load, beat.Reps));
        Assert.Equal(["Last top set at RPE 7.5: one step on."], aim.Reasons);
    }

    [Fact]
    public void With_nothing_said_about_the_last_session_the_aim_still_steps()
    {
        var aim = PushAim(BenchedThreeDaysAgo());

        Assert.IsType<LiftAim.Beat>(aim.Aim);
        Assert.Equal(["Nothing says hold: one step on from last time's best set."], aim.Reasons);
    }

    [Fact]
    public void A_going_in_rating_under_the_usual_day_holds_last_times_set()
    {
        var aim = PushAim(BenchedThreeDaysAgo(rpe: Rpe.Of(7)), new GoingInRating.Rated(58, GoingInBand.Reduced));

        var hold = Assert.IsType<LiftAim.Hold>(aim.Aim);
        Assert.Equal((200d, 5), (hold.Load, hold.Reps));
        Assert.Equal(["Going-in rating 58, under your usual day: hold the weight."], aim.Reasons);
    }

    [Fact]
    public void A_usual_going_in_rating_does_not_hold_it()
    {
        Assert.IsType<LiftAim.Beat>(PushAim(BenchedThreeDaysAgo(), new GoingInRating.Rated(72, GoingInBand.Normal)).Aim);
    }

    [Fact]
    public void A_last_top_set_at_rpe_nine_and_a_half_holds_it()
    {
        var aim = PushAim(BenchedThreeDaysAgo(rpe: Rpe.Of(9.5)));

        Assert.IsType<LiftAim.Hold>(aim.Aim);
        Assert.Equal(["Last top set at RPE 9.5: hold it until it moves easier."], aim.Reasons);
    }

    [Fact]
    public void A_last_session_rated_hardest_holds_it()
    {
        Assert.IsType<LiftAim.Hold>(PushAim(BenchedThreeDaysAgo(difficulty: 5)).Aim);
    }

    [Fact]
    public void Fatigue_of_four_at_the_weeks_latest_session_holds_it_even_on_another_family()
    {
        var log = BenchedThreeDaysAgo(rpe: Rpe.Of(7)).WithRatedSession(Today.AddDays(-1), fatigue: 4, difficulty: null, Sets(4, Row));

        var aim = PushAim(log);

        Assert.IsType<LiftAim.Hold>(aim.Aim);
        Assert.Equal(["Accumulated fatigue rated 4 of 5 this week: hold the weight."], aim.Reasons);
    }

    [Fact]
    public void Fatigue_rated_before_the_week_does_not_hold_it()
    {
        var log = Catalogue
            .WithRatedSession(Today.AddDays(-9), fatigue: 5, difficulty: null, Set(Bench, 195, 5))
            .WithSession(Today.AddDays(-3), Set(Bench, 200, 5));

        Assert.IsType<LiftAim.Beat>(PushAim(log).Aim);
    }

    [Fact]
    public void A_pinned_target_states_how_far_the_aim_is_from_it()
    {
        var target = ResultAssert.Succeeded(FocusTarget.Lift.Of(225, WeightUnit.Pounds, 5));

        var aim = PushAim(BenchedThreeDaysAgo(), pins: [BenchPinned(target)]);

        Assert.Equal("Pinned target 5 × 225 lb: 20 lb to go.", aim.Reasons[^1]);
    }

    [Fact]
    public void A_pinned_target_the_aim_reaches_is_said_to_be_met()
    {
        var target = ResultAssert.Succeeded(FocusTarget.Lift.Of(205, WeightUnit.Pounds, 5));

        Assert.Equal("Pinned target 5 × 205 lb: this set meets it.", PushAim(BenchedThreeDaysAgo(), pins: [BenchPinned(target)]).Reasons[^1]);
    }

    [Fact]
    public void A_pins_sessions_a_week_is_the_lifts_frequency_prorated_to_the_days_available()
    {
        var off = Scoring.Off(Today.AddDays(-6), Today.AddDays(-5), Today.AddDays(-4), Today.AddDays(-2));

        var aim = PushAim(BenchedThreeDaysAgo(), pins: [BenchPinned(new FocusTarget.None(), sessionsPerWeek: 3)], days: off);

        var sessions = Assert.IsType<PriorityFrequency.Asked>(aim.Frequency).Sessions;
        Assert.Equal((1d, 3d, 2d), (sessions.Done, sessions.Raw.Target, sessions.Adjusted.Target));
        Assert.Equal("Pinned for 3 sessions a week.", sessions.Reasons[0]);
    }

    [Fact]
    public void An_unpinned_priority_counts_its_sessions_with_nothing_asked()
    {
        Assert.Equal(new PriorityFrequency.NotAsked(1), PushAim(BenchedThreeDaysAgo()).Frequency);
    }
}
