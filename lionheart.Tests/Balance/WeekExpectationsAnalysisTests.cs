using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Score;
using static lionheart.Tests.Balance.ALog;

namespace lionheart.Tests.Balance;

/// <summary>
/// The week's expectations and the adjusted score: one target per measurement that every reader
/// takes, prorated to the days not marked non-training, with its reasons, and a second score that
/// equals the first until a day is marked off.
/// </summary>
public class WeekExpectationsAnalysisTests
{
    private static readonly DateOnly Sunday = new(2026, 11, 8);

    private static readonly CardioMeasure SomeCardio = new CardioMeasure.Measured(60, 10, 1, 2, 7);

    private static Func<DayRange, CardioMeasure> Every(CardioMeasure measure) => _ => measure;

    /// <summary>Five hard sets of push, pull and legs a week for four weeks, then this week's three sessions.</summary>
    private static ALog FourSteadyWeeksAndThisOne(int setsThisWeek)
    {
        var log = Catalogue;
        foreach (var week in new[] { 7, 14, 21, 28 })
        {
            log = log
                .WithSession(Sunday.AddDays(-week - 1), Sets(5, Bench))
                .WithSession(Sunday.AddDays(-week - 3), Sets(5, Row))
                .WithSession(Sunday.AddDays(-week - 5), Sets(5, Squat));
        }

        return log
            .WithSession(Sunday.AddDays(-5), Sets(setsThisWeek, Bench))
            .WithSession(Sunday.AddDays(-3), Sets(setsThisWeek, Row))
            .WithSession(Sunday.AddDays(-1), Sets(setsThisWeek, Squat));
    }

    [Fact]
    public void With_every_day_open_each_adjusted_target_is_the_raw_one_and_the_adjusted_score_is_the_score()
    {
        var score = Scoring.Score(FourSteadyWeeksAndThisOne(4).Build(), [], Every(SomeCardio), Sunday);

        Assert.All(score.Expectations.Families, family => Assert.Equal(family.Sets.Raw, family.Sets.Adjusted));
        Assert.All(score.Expectations.Cardio.Zones, zone => Assert.Equal(zone.Minutes.Raw, zone.Minutes.Adjusted));
        Assert.Equal((score.Value, score.OutOf), (score.Adjusted.Value, score.Adjusted.OutOf));
        Assert.Equal(7, score.Adjusted.AvailableDays);
    }

    [Fact]
    public void A_family_target_states_what_it_grew_from()
    {
        var push = Scoring.Score(FourSteadyWeeksAndThisOne(4).Build(), [], Every(SomeCardio), Sunday)
            .Expectations.Families.Single(family => family.Family == PatternFamily.Push);

        Assert.Equal((4d, 6d), (push.Sets.Done, push.Sets.Raw.Target));
        Assert.Equal(["4-week average 5 hard sets, +10 %: 6."], push.Sets.Reasons);
    }

    [Fact]
    public void Two_days_off_lower_every_target_to_its_share_of_the_week_rounded_up_and_say_why()
    {
        var off = Scoring.Off(Sunday.AddDays(-4), Sunday.AddDays(-2));

        var expected = Scoring.Score(FourSteadyWeeksAndThisOne(4).Build(), [], Every(SomeCardio), Sunday, off).Expectations;

        var push = expected.Families.Single(family => family.Family == PatternFamily.Push).Sets;
        // Six sets over five days of seven is 4.3, rounded up to the next whole set.
        Assert.Equal((6d, 5d), (push.Raw.Target, push.Adjusted.Target));
        Assert.Equal("Lowered to 5: 2 of 7 days marked non-training (travel).", push.Reasons[^1]);
        // Three sessions over five days is 2.1, and a share of a session is still a session asked.
        Assert.Equal(3d, expected.Sessions.Adjusted.Target);
        Assert.Equal(5, expected.Availability.AvailableDays);
    }

    [Fact]
    public void A_constrained_week_done_as_well_as_its_days_allowed_scores_higher_adjusted_than_raw()
    {
        var off = Scoring.Off(Sunday.AddDays(-6), Sunday.AddDays(-4), Sunday.AddDays(-2));

        var score = Scoring.Score(FourSteadyWeeksAndThisOne(4).Build(), [], Every(SomeCardio), Sunday, off);

        Assert.True(score.Adjusted.Value > score.Value);
        Assert.Equal(score.OutOf, score.Adjusted.OutOf);
        Assert.Equal(4, score.Adjusted.AvailableDays);
    }

    [Fact]
    public void A_week_wholly_marked_off_asks_for_nothing_and_takes_those_points_out_of_reach()
    {
        var week = Enumerable.Range(0, 7).Select(day => Sunday.AddDays(-day)).ToArray();

        var score = Scoring.Score(FourSteadyWeeksAndThisOne(0).Build(), [], Every(SomeCardio), Sunday, Scoring.Off(week));

        Assert.All(score.Expectations.Families, family => Assert.Equal(0d, family.Sets.Adjusted.Target));
        Assert.Equal("Nothing asked: all 7 days marked non-training.", score.Expectations.Sessions.Reasons[^1]);
        // Lifting and cardio ask nothing of the week, nor does its consistency block: 35 + 25 + 5.
        Assert.Equal(100 - 65, score.Adjusted.OutOf);
        Assert.Equal(100, score.OutOf);
    }

    [Fact]
    public void Proration_rounds_each_figure_up_to_its_step_and_leaves_a_full_week_alone()
    {
        var target = new WeeklyTarget(110, 150, 60, 100);

        Assert.Equal(target, Progression.Prorated(target, ScoreRules.Zone2, 7));
        Assert.Equal(new WeeklyTarget(80, 110, 45, 100), Progression.Prorated(target, ScoreRules.Zone2, 5));
        Assert.Equal(new WeeklyTarget(0, 0, 0, 100), Progression.Prorated(target, ScoreRules.Zone2, 0));
    }

    [Fact]
    public void The_volume_screen_reads_each_family_against_the_adjusted_expectation()
    {
        var log = FourSteadyWeeksAndThisOne(4).Build();
        var off = Scoring.Off(Sunday.AddDays(-4), Sunday.AddDays(-2));
        var score = Scoring.Score(log, [], Every(SomeCardio), Sunday, off);

        var volume = lionheart.Services.Training.Balance.TrainingVolumeAnalysis.Build(
            log, Sunday, new lionheart.Model.Training.Balance.ReadinessNote.NotRead(), score.Expectations.Families);

        Assert.All(volume.Families, family => Assert.Equal(5, family.Target));
    }
}
