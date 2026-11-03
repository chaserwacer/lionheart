using lionheart.Model.Training.Focus;
using lionheart.Model.Training.Patterns;
using lionheart.Services.Training.Focus;
using lionheart.Tests.Support;
using static lionheart.Tests.Balance.ALog;

namespace lionheart.Tests.Balance;

/// <summary>
/// A family's priority lift: the pin in force, else the most-trained compound lift; its window's
/// best against the twelve weeks before, on system load where its weight adds to bodyweight.
/// </summary>
public class PriorityAnalysisTests
{
    private static readonly DateOnly Sunday = new(2026, 11, 8);

    private static PriorityLift.Chosen Push(ALog log, params FocusPin[] pins) =>
        Assert.IsType<PriorityLift.Chosen>(PriorityAnalysis.Read(log.Build(), pins, Sunday)
            .Single(family => family.Family == PatternFamily.Push).Lift);

    private static FocusPin PushPriority(Guid movement, DateOnly starts, DateOnly ends) =>
        FocusPin.Pin(
            Guid.NewGuid(), Guid.NewGuid(), movement,
            ResultAssert.Succeeded(FocusRole.Priority.For(PatternFamily.Push)),
            ResultAssert.Succeeded(FocusTerms.Of(
                ResultAssert.Succeeded(FocusHorizon.Of(starts, ends)), 2, new FocusTarget.None())),
            DateTime.UnixEpoch);

    [Fact]
    public void The_pinned_priority_is_read_even_when_another_push_lift_was_trained_more()
    {
        var log = Catalogue
            .WithSession(Sunday.AddDays(-10), Set(Bench), Set(Dip))
            .WithSession(Sunday.AddDays(-3), Set(Bench));

        var push = Push(log, PushPriority(Dip, Sunday.AddDays(-30), Sunday.AddDays(30)));

        Assert.Equal(Dip, push.MovementDataId);
        Assert.IsType<PriorityChoice.ByPin>(push.Choice);
        Assert.IsType<LiftProgress.NotTrained>(push.Progress);
    }

    [Fact]
    public void With_no_pin_the_most_trained_compound_lift_of_eight_weeks_stands_in_and_isolation_never_does()
    {
        var log = Catalogue
            .WithSession(Sunday.AddDays(-40), Set(Bench), Set(Raise))
            .WithSession(Sunday.AddDays(-20), Set(Bench), Set(Raise))
            .WithSession(Sunday.AddDays(-10), Set(Dip), Set(Raise));

        var push = Push(log);

        Assert.Equal(Bench, push.MovementDataId);
        Assert.Equal(new PriorityChoice.MostTrained(2), push.Choice);
    }

    [Fact]
    public void A_pin_let_go_before_the_day_is_not_in_force()
    {
        var pin = PushPriority(Dip, Sunday.AddDays(-30), Sunday.AddDays(30));
        ResultAssert.Succeeded(pin.Unpin(Sunday.AddDays(-1)));

        Assert.False(PriorityAnalysis.InForce(pin, Sunday));
        Assert.True(PriorityAnalysis.InForce(pin, Sunday.AddDays(-1)));
    }

    [Fact]
    public void A_best_above_the_twelve_weeks_before_is_a_new_best_and_says_whether_it_is_a_lifetime_one()
    {
        var log = Catalogue
            .WithSession(Sunday.AddDays(-200), Set(Bench, 250, 5))
            .WithSession(Sunday.AddDays(-30), Set(Bench, 200, 5))
            .WithSession(Sunday.AddDays(-2), Set(Bench, 210, 5));

        var progress = Assert.IsType<LiftProgress.Compared>(Push(log).Progress);

        Assert.True(progress.IsNewBest);
        Assert.False(progress.IsLifetimeBest);
        Assert.Equal(210 * (1 + 5 / 30.0), progress.Best, precision: 6);
        Assert.Equal(250 * (1 + 5 / 30.0), progress.AllTimeBest, precision: 6);
    }

    [Fact]
    public void Trained_with_nothing_in_the_twelve_weeks_before_is_first_back()
    {
        var log = Catalogue
            .WithSession(Sunday.AddDays(-120), Set(Bench, 250, 5))
            .WithSession(Sunday.AddDays(-1), Set(Bench, 200, 5));

        Assert.IsType<LiftProgress.FirstBack>(Push(log, PushPriority(Bench, Sunday.AddDays(-7), Sunday)).Progress);
    }

    [Fact]
    public void Sets_past_twelve_reps_or_at_no_load_estimate_nothing()
    {
        var log = Catalogue.WithSession(Sunday.AddDays(-1), Set(Bench, 135, 15), Set(Bench, 0, 8));

        Assert.IsType<LiftProgress.NoEstimate>(Push(log, PushPriority(Bench, Sunday.AddDays(-7), Sunday)).Progress);
    }

    [Fact]
    public void A_dip_is_estimated_on_bodyweight_plus_plate_with_the_first_weighing_standing_in_before_it()
    {
        var log = Catalogue
            .WithBodyweight(Sunday.AddDays(-20), 175)
            .WithSession(Sunday.AddDays(-40), Set(Dip, 40, 5))
            .WithSession(Sunday.AddDays(-1), Set(Dip, 45, 5));

        var dip = Push(log, PushPriority(Dip, Sunday.AddDays(-60), Sunday));

        var progress = Assert.IsType<LiftProgress.Compared>(dip.Progress);
        Assert.True(progress.OnSystemLoad);
        Assert.Equal((175 + 45) * (1 + 5 / 30.0), progress.Best, precision: 6);
        Assert.Equal((175 + 40) * (1 + 5 / 30.0), progress.PriorBest, precision: 6);
        Assert.Equal([215d, 220d], dip.Series.Select(best => best.Load));
    }

    [Fact]
    public void A_family_with_no_pin_and_no_compound_lift_in_eight_weeks_has_no_lift() =>
        Assert.IsType<PriorityLift.NoLift>(PriorityAnalysis.Read(
                Catalogue.WithSession(Sunday.AddDays(-90), Set(Squat)).Build(), [], Sunday)
            .Single(family => family.Family == PatternFamily.Legs).Lift);
}
