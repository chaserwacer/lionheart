using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Model.Training.Balance;
using lionheart.Model.Training.Expectations;
using lionheart.Model.Training.Load;
using lionheart.Model.Training.Next;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Score;
using lionheart.Services.Training.Expectations;
using lionheart.Services.Training.Load;
using lionheart.Services.Training.Next;
using lionheart.Services.Training.Score;
using static lionheart.Tests.Balance.ALog;

namespace lionheart.Tests.Balance;

/// <summary>
/// What the load reads do to the week every screen reads: a suggested deload, an injury, everyday
/// load and a spike reach the adjusted targets, the next session and the week's one piece of advice,
/// and never the raw targets the score reads.
/// </summary>
public class LoadManagementTests
{
    private static readonly DateOnly Monday = new(2026, 11, 9);
    private static readonly DateOnly Thursday = Monday.AddDays(3);

    private static readonly Func<DayRange, CardioMeasure> NoCardio = _ => new CardioMeasure.Unknown(CardioUnknownReason.NoHeartRate);

    private static ALog Week(ALog log, int weeksBack, int setsEach)
    {
        var sunday = Monday.AddDays(-1 - 7 * (weeksBack - 1));
        return log
            .WithSession(sunday.AddDays(-5), Sets(setsEach, Bench))
            .WithSession(sunday.AddDays(-3), Sets(setsEach, Row))
            .WithSession(sunday.AddDays(-1), Sets(setsEach, Squat));
    }

    /// <summary>Four building weeks of a steep build before the Monday: a deload is suggested for its week.</summary>
    private static ALog ASteepBuild() =>
        new[] { (5, 3), (4, 6), (3, 9), (2, 12), (1, 15) }.Aggregate(Catalogue, (log, week) => Week(log, week.Item1, week.Item2));

    /// <summary>Eight steady weeks of six sets a family, which no rule but keep building speaks to.</summary>
    private static ALog SteadyWeeks() =>
        Enumerable.Range(1, 8).Aggregate(Catalogue, (log, back) => Week(log, back, 6));

    private static ExpectationInputs Inputs(ALog log, Func<DayRange, CardioMeasure>? cardio = null) =>
        Scoring.Inputs(log.Build(), [], cardio ?? NoCardio);

    private static TrainingScore Score(ExpectationInputs inputs, DateOnly day) => TrainingScoreAnalysis.Score(inputs, day);

    private static InjuryRecord AShoulder(DateOnly since, bool active = true, PatternFamily[]? families = null, Guid[]? movements = null) =>
        new(Guid.NewGuid(), "the shoulder", since, active, families ?? [PatternFamily.Push], movements ?? [], 3);

    private static FamilyExpectation Family(TrainingScore score, PatternFamily family) =>
        score.Expectations.Families.Single(expected => expected.Family == family);

    // ---- Deload ----

    [Fact]
    public void A_suggested_deload_asks_each_family_its_share_in_the_adjusted_target_and_leaves_the_raw_one()
    {
        var push = Family(Score(Inputs(ASteepBuild()), Thursday), PatternFamily.Push).Sets;

        Assert.Equal(Math.Ceiling(LoadAnalysis.DeloadShare * push.Raw.Target), push.Adjusted.Target);
        Assert.StartsWith("Deload week suggested:", push.Reasons[^1]);
    }

    [Fact]
    public void A_lighter_week_that_follows_the_deload_scores_higher_adjusted_than_raw()
    {
        var log = ASteepBuild()
            .WithSession(Monday, Sets(4, Bench))
            .WithSession(Monday.AddDays(1), Sets(4, Row))
            .WithSession(Monday.AddDays(2), Sets(4, Squat));

        var score = Score(Inputs(log), Monday.AddDays(6));

        Assert.True(Assert.IsType<Deload.Suggested>(score.Expectations.Load.Lifting.Deload).Following);
        Assert.True(score.Adjusted.Value > score.Value);
    }

    [Fact]
    public void A_deload_week_holds_the_priority_lifts_top_set()
    {
        var aim = Assert.Single(Score(Inputs(ASteepBuild()), Thursday).Expectations.Priorities, priority => priority.Family == PatternFamily.Push);

        Assert.IsType<LiftAim.Hold>(aim.Aim);
        Assert.Contains("Deload week suggested: hold the top set and do fewer sets.", aim.Reasons);
    }

    // ---- Injuries ----

    [Fact]
    public void An_active_injury_pauses_its_family_so_the_adjusted_score_holds_nothing_against_it()
    {
        var inputs = Inputs(SteadyWeeks()) with { Injuries = [AShoulder(Monday)] };

        var score = Score(inputs, Thursday);
        var push = Family(score, PatternFamily.Push);

        Assert.True(push.Paused);
        Assert.Equal(0, push.Sets.Adjusted.Committed);
        Assert.Equal("Paused while the shoulder is managed: hold it near 6 hard sets a week, your 4-week average, rather than building on it.", push.Sets.Reasons[^1]);
        Assert.Equal(0, score.Adjusted.Parts.Single(part => part.Part == ScorePart.Lifting).OutOf - 2 * ScoreRules.LiftingPoints / 3.0, precision: 6);
        Assert.NotEqual(0, push.Sets.Raw.Committed);
    }

    [Fact]
    public void An_injury_named_with_no_family_still_pauses_the_family_of_a_movement_its_check_ins_name()
    {
        var inputs = Inputs(SteadyWeeks()) with { Injuries = [AShoulder(Monday, families: [], movements: [Squat])] };

        var managed = Assert.Single(Score(inputs, Thursday).Expectations.Injuries);

        Assert.Equal([PatternFamily.Legs], managed.Families);
    }

    [Fact]
    public void A_resolved_injury_and_one_dated_after_the_window_manage_nothing()
    {
        var inputs = Inputs(SteadyWeeks()) with { Injuries = [AShoulder(Monday, active: false), AShoulder(Thursday.AddDays(1))] };

        Assert.Empty(Score(inputs, Thursday).Expectations.Injuries);
    }

    [Fact]
    public void An_injury_carries_the_trend_that_led_up_to_it()
    {
        // Six sets a family for eight weeks before the four that rose to twelve.
        var log = Enumerable.Range(1, 12).Aggregate(Catalogue, (built, back) => Week(built, back, back <= 4 ? 12 : 6));
        var inputs = Inputs(log) with { Injuries = [AShoulder(Monday)] };

        var trend = Assert.Single(Assert.Single(Score(inputs, Thursday).Expectations.Injuries).Preceding);

        Assert.Equal((PatternFamily.Push, 12d, 6d, 1d), (trend.Family, trend.Recent, trend.Earlier, trend.Change));
    }

    [Fact]
    public void Up_next_passes_over_a_family_an_injury_pauses()
    {
        // Push was trained longest ago, and is paused.
        var log = Catalogue
            .WithSession(Thursday.AddDays(-6), Sets(4, Bench))
            .WithSession(Thursday.AddDays(-4), Sets(4, Row))
            .WithSession(Thursday.AddDays(-2), Sets(4, Squat));
        var inputs = Inputs(log) with { Injuries = [AShoulder(Monday, movements: [Bench])] };
        var built = log.Build();

        var next = NextSessionsAnalysis.Of(new NextInputs(Thursday, built, Score(inputs, Thursday), [], RecoveryRead.NotRead));

        var lift = Assert.IsType<NextLift.Train>(next.Lift);
        Assert.Equal([PatternFamily.Pull], lift.Families);
        Assert.Contains("Push paused while the shoulder is managed.", lift.Reasons);
        Assert.Contains("Leave out Bench Press while the shoulder is managed.", lift.Reasons);
    }

    // ---- Everyday load ----

    private static EverydayLoad FarAbove(params DateOnly[] days) =>
        new([.. days.Select(day => new EverydayDay(day, 1200, new EverydayStanding.Judged(500, 700, EverydayBand.FarAbove)))]);

    [Fact]
    public void Three_days_far_above_his_usual_lower_the_adjusted_targets_and_say_why_without_touching_the_raw()
    {
        var inputs = Inputs(SteadyWeeks()) with { Everyday = FarAbove(Monday, Monday.AddDays(1), Monday.AddDays(2)) };

        var push = Family(Score(inputs, Thursday), PatternFamily.Push).Sets;

        // A target of 7 over the 5.5 days left of 7 is 5.5, rounded up.
        Assert.Equal((7d, 6d), (push.Raw.Target, push.Adjusted.Target));
        Assert.Equal("Lowered to 6: 3 days far above your usual activity.", push.Reasons[^1]);
    }

    [Fact]
    public void A_single_day_far_above_his_usual_moves_no_target()
    {
        var inputs = Inputs(SteadyWeeks()) with { Everyday = FarAbove(Monday) };

        var push = Family(Score(inputs, Thursday), PatternFamily.Push).Sets;

        Assert.Equal(push.Raw, push.Adjusted);
    }

    [Fact]
    public void A_day_far_above_his_usual_yesterday_turns_the_next_cardio_into_shorter_zone_2()
    {
        var window = VolumeTargets.WindowEndingOn(Thursday);
        CardioMeasure Cardio(DayRange range) =>
            range == window ? new CardioMeasure.Measured(0, 0, 0, 0, 7) : new CardioMeasure.Measured(150, 30, 2, 4, 7);
        var inputs = Inputs(SteadyWeeks(), Cardio) with { Everyday = FarAbove(Thursday.AddDays(-1)) };

        var next = NextSessionsAnalysis.Of(new NextInputs(Thursday, inputs.Log, Score(inputs, Thursday), [], RecoveryRead.NotRead));

        var zone2 = Assert.IsType<NextCardio.Zone2>(next.Cardio);
        Assert.Equal(NextSessionsAnalysis.EasedMinutes, zone2.Minutes);
        Assert.Contains(zone2.Reasons, reason => reason.StartsWith("Yesterday was far above your usual activity"));
    }

    // ---- Weekly advice ----

    [Fact]
    public void The_weeks_advice_reads_the_same_every_day_of_the_week_whatever_the_week_brings()
    {
        var log = ASteepBuild().WithSession(Monday.AddDays(2), Sets(30, Bench));

        var advice = Enumerable.Range(0, 7).Select(day => Score(Inputs(log), Monday.AddDays(day)).Expectations.Advice).ToList();

        Assert.All(advice, each =>
        {
            Assert.Equal((advice[0].GetType(), advice[0].Headline), (each.GetType(), each.Headline));
            Assert.Equal(advice[0].Reasons, each.Reasons);
        });
        Assert.IsType<WeeklyAdvice.TakeDeload>(advice[0]);
    }

    [Fact]
    public void An_injury_dated_by_the_monday_comes_before_a_deload_and_names_what_led_up_to_it()
    {
        var inputs = Inputs(ASteepBuild()) with { Injuries = [AShoulder(Monday)] };

        var advice = Assert.IsType<WeeklyAdvice.ManageInjury>(Score(inputs, Thursday).Expectations.Advice);

        Assert.Equal("Hold push while the shoulder settles", advice.Headline);
        Assert.Contains(advice.Reasons, reason => reason.StartsWith("Push volume rose"));
    }

    [Fact]
    public void An_injury_dated_after_the_monday_waits_for_next_weeks_advice()
    {
        var inputs = Inputs(ASteepBuild()) with { Injuries = [AShoulder(Monday.AddDays(1))] };

        Assert.IsType<WeeklyAdvice.TakeDeload>(Score(inputs, Thursday).Expectations.Advice);
    }

    [Fact]
    public void Last_weeks_running_far_over_the_four_before_holds_this_weeks_running()
    {
        var lastWeek = new DayRange(Monday.AddDays(-7), Monday.AddDays(-1));
        CardioMeasure Cardio(DayRange range) => new CardioMeasure.Measured(100, 0, 0, 3, 7)
        {
            Minutes = 200,
            RunningMeters = range == lastWeek ? 32_000 : 16_000,
        };

        var advice = Assert.IsType<WeeklyAdvice.EaseRamp>(Score(Inputs(SteadyWeeks(), Cardio), Thursday).Expectations.Advice);

        Assert.Equal((RampedMeasure.Running, "Hold your running this week"), (advice.Measure, advice.Headline));
    }

    [Fact]
    public void Two_days_far_above_his_usual_last_week_ease_this_one_in()
    {
        var inputs = Inputs(SteadyWeeks()) with { Everyday = FarAbove(Monday.AddDays(-3), Monday.AddDays(-2)) };

        var advice = Assert.IsType<WeeklyAdvice.EaseAfterEverydayLoad>(Score(inputs, Thursday).Expectations.Advice);

        Assert.Equal(2, advice.FarAboveDays);
    }

    [Fact]
    public void A_week_of_full_lifting_and_little_cardio_asks_for_more_cardio()
    {
        CardioMeasure Cardio(DayRange range) => new CardioMeasure.Measured(20, 0, 0, 1, 7) { Minutes = 20 };
        // Six sets a family for weeks, then a week of eight: last week's targets of seven met in full.
        var log = Week(Enumerable.Range(2, 8).Aggregate(Catalogue, (built, back) => Week(built, back, 6)), 1, 8);

        Assert.IsType<WeeklyAdvice.EmphasiseCardio>(Score(Inputs(log, Cardio), Thursday).Expectations.Advice);
    }

    [Fact]
    public void A_priority_trained_every_week_of_the_last_four_without_a_new_best_has_stalled()
    {
        var log = Enumerable.Range(1, 14).Aggregate(Catalogue, (built, back) =>
            built.WithSession(Monday.AddDays(-2 - 7 * (back - 1)), Sets(3, Bench, 200, 5)));

        var advice = Assert.IsType<WeeklyAdvice.PriorityStalled>(Score(Inputs(log), Thursday).Expectations.Advice);

        Assert.Equal((PatternFamily.Push, "Bench Press"), (advice.Family, advice.Lift));
    }

    [Fact]
    public void With_nothing_else_to_say_the_advice_is_keep_building_naming_what_fell_furthest_behind()
    {
        var lighterLegs = Enumerable.Range(1, 8).Aggregate(Catalogue, (built, back) =>
        {
            // Every lift a little heavier each week, so no priority reads stalled.
            var sunday = Monday.AddDays(-1 - 7 * (back - 1));
            var pounds = 200 - 5 * back;
            return built
                .WithSession(sunday.AddDays(-5), Sets(6, Bench, pounds))
                .WithSession(sunday.AddDays(-3), Sets(6, Row, pounds))
                .WithSession(sunday.AddDays(-1), Sets(back is 1 ? 2 : 6, Squat, pounds));
        });

        var advice = Assert.IsType<WeeklyAdvice.KeepBuilding>(Score(Inputs(lighterLegs), Thursday).Expectations.Advice);

        Assert.Equal("Furthest behind last week: legs, 2 of 7 hard sets.", advice.Reasons[0]);
    }
}
