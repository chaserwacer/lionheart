using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Model.Training.Balance;
using lionheart.Model.Training.Next;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Sets;
using lionheart.Services.Training.Focus;
using lionheart.Services.Training.Next;
using lionheart.Services.Training.Score;
using static lionheart.Tests.Balance.ALog;

namespace lionheart.Tests.Balance;

/// <summary>
/// What to train next: the family due with its priority's next top set, a quality session or zone
/// 2, and which first given recovery and the interference between legs and intervals.
/// </summary>
public class NextSessionsAnalysisTests
{
    private static readonly DateOnly Today = new(2026, 11, 4);
    private static readonly DayRange Window = new(Today.AddDays(-6), Today);

    /// <summary>Push six days ago, pull four, legs two: push is due.</summary>
    private static ALog AWeek() => Catalogue
        .WithSession(Today.AddDays(-6), Sets(4, Bench, 200, 5))
        .WithSession(Today.AddDays(-4), Sets(4, Row, 150, 8))
        .WithSession(Today.AddDays(-2), Sets(4, Squat, 300, 5));

    /// <summary>Twenty quality minutes a week before; none this week.</summary>
    private static CardioMeasure CardioOf(DayRange range) =>
        range == Window ? new CardioMeasure.Measured(30, 0, 0, 1, 7) : new CardioMeasure.Measured(90, 20, 1, 3, 7);

    private static readonly RecoveryRead Rested =
        new(new ReadinessNote.Usual(80), 450, 70, 68, 8);

    private static NextSessions Next(ALog log, RecoveryRead? recovery = null, IReadOnlyList<CardioSession>? cardio = null)
    {
        var built = log.Build();
        return NextSessionsAnalysis.Of(new NextInputs(
            Today,
            built,
            PriorityAnalysis.Read(built, [], Today),
            TrainingScoreAnalysis.Score(built, [], CardioOf, Today),
            cardio ?? [],
            recovery ?? Rested));
    }

    [Fact]
    public void The_lift_is_the_family_trained_longest_ago_with_its_priority_one_step_past_its_last_best_set()
    {
        var lift = Assert.IsType<NextLift.Train>(Next(AWeek()).Lift);

        Assert.Equal([PatternFamily.Push], lift.Families);
        var aim = Assert.IsType<LiftAim.Beat>(Assert.Single(lift.Priorities).Aim);
        Assert.Equal((205d, 5, WeightUnit.Pounds, false), (aim.Load, aim.Reps, aim.Unit, aim.OnSystemLoad));
        Assert.Equal(205 * (1 + 5 / 30.0), aim.EstimatedOneRepMax, precision: 6);
        Assert.Equal(200 * (1 + 5 / 30.0), aim.BestToBeat, precision: 6);
    }

    [Fact]
    public void A_bodyweight_priority_steps_by_a_rep_on_the_same_system_load()
    {
        var log = Catalogue.WithBodyweight(Today.AddDays(-30), 180)
            .WithSession(Today.AddDays(-6), Set(Dip, 0, 8), Set(Dip, 0, 8))
            .WithSession(Today.AddDays(-4), Sets(4, Row))
            .WithSession(Today.AddDays(-2), Sets(4, Squat));

        var aim = Assert.IsType<LiftAim.Beat>(Assert.Single(Assert.IsType<NextLift.Train>(Next(log).Lift).Priorities).Aim);

        Assert.Equal((180d, 9, true), (aim.Load, aim.Reps, aim.OnSystemLoad));
    }

    [Fact]
    public void With_the_weeks_quality_unmet_and_none_in_two_days_the_cardio_is_intervals_sized_to_the_target()
    {
        var cardio = Assert.IsType<NextCardio.Quality>(Next(AWeek()).Cardio);

        // Twenty a week before grows to a target of 25: three bouts of eight.
        Assert.Equal((3, 8, 2), (cardio.Bouts, cardio.BoutMinutes, cardio.RecoveryMinutes));
    }

    [Fact]
    public void A_quality_session_yesterday_keeps_the_next_cardio_easy()
    {
        var next = Next(AWeek(), cardio: [Intervals(Today.AddDays(-1))]);

        Assert.IsType<NextCardio.Zone2>(next.Cardio);
        Assert.Contains(next.Cardio.Reasons, reason => reason.Contains("48 hours"));
    }

    [Fact]
    public void Low_readiness_puts_easy_aerobic_work_first_and_takes_intervals_off_the_table()
    {
        var next = Next(AWeek(), Rested with { Readiness = new ReadinessNote.Low(55, 70, 88) });

        Assert.Equal(NextKind.Cardio, next.First.Session);
        Assert.Equal(30, Assert.IsType<NextCardio.Zone2>(next.Cardio).Minutes);
    }

    [Theory]
    [InlineData(300, 70, 68, 8)] // five hours asleep
    [InlineData(450, 50, 68, 8)] // HRV more than a spread under its usual
    public void A_short_night_or_a_low_hrv_reads_as_low_recovery(int sleep, int hrv, double usual, double spread) =>
        Assert.NotEmpty(NextSessionsAnalysis.RecoveryNotes(new RecoveryRead(new ReadinessNote.Usual(80), sleep, hrv, usual, spread)));

    [Fact]
    public void Legs_yesterday_put_lifting_before_hard_intervals()
    {
        var log = Catalogue
            .WithSession(Today.AddDays(-6), Sets(4, Bench))
            .WithSession(Today.AddDays(-4), Sets(4, Row))
            .WithSession(Today.AddDays(-1), Sets(4, Squat));

        var next = Next(log);

        Assert.IsType<NextCardio.Quality>(next.Cardio);
        Assert.Equal(NextKind.Lift, next.First.Session);
    }

    [Fact]
    public void Intervals_yesterday_put_easy_cardio_before_heavy_legs()
    {
        var log = Catalogue
            .WithSession(Today.AddDays(-6), Sets(4, Squat))
            .WithSession(Today.AddDays(-4), Sets(4, Bench))
            .WithSession(Today.AddDays(-3), Sets(4, Row));

        var next = Next(log, cardio: [Intervals(Today.AddDays(-1))]);

        Assert.Equal([PatternFamily.Legs], Assert.IsType<NextLift.Train>(next.Lift).Families);
        Assert.Equal(NextKind.Cardio, next.First.Session);
    }

    [Fact]
    public void Having_lifted_today_the_next_session_is_cardio()
    {
        var log = AWeek().WithSession(Today, Sets(4, Bench));

        Assert.Equal(NextKind.Cardio, Next(log).First.Session);
    }

    [Fact]
    public void A_due_lift_not_trained_this_week_keeps_its_own_unit()
    {
        // Bench last done eight days ago, in kilograms: due, and outside the window.
        var kilos = Set(Bench, 90, 5) with { Unit = WeightUnit.Kilograms };
        var log = Catalogue
            .WithSession(Today.AddDays(-8), kilos, kilos)
            .WithSession(Today.AddDays(-4), Sets(4, Row))
            .WithSession(Today.AddDays(-2), Sets(4, Squat));

        var aim = Assert.IsType<LiftAim.Beat>(Assert.Single(Assert.IsType<NextLift.Train>(Next(log).Lift).Priorities).Aim);

        Assert.Equal((92.5, 5, WeightUnit.Kilograms), (aim.Load, aim.Reps, aim.Unit));
    }

    [Fact]
    public void Legs_today_keep_the_next_cardio_easy_and_put_it_next()
    {
        var log = Catalogue
            .WithSession(Today.AddDays(-6), Sets(4, Bench))
            .WithSession(Today.AddDays(-4), Sets(4, Row))
            .WithSession(Today, Sets(4, Squat));

        var next = Next(log);

        Assert.IsType<NextCardio.Zone2>(next.Cardio);
        Assert.Equal(NextKind.Cardio, next.First.Session);
    }

    [Fact]
    public void Intervals_today_with_legs_due_put_legs_tomorrow_rather_than_straight_after()
    {
        var log = Catalogue
            .WithSession(Today.AddDays(-6), Sets(4, Squat))
            .WithSession(Today.AddDays(-4), Sets(4, Bench))
            .WithSession(Today.AddDays(-3), Sets(4, Row));

        var next = Next(log, cardio: [Intervals(Today)]);

        Assert.Equal(NextKind.Lift, next.First.Session);
        Assert.Contains(next.First.Reasons, reason => reason.Contains("tomorrow"));
    }

    [Fact]
    public void Unread_cardio_neither_owes_intervals_nor_decides_which_first()
    {
        var built = AWeek().Build();
        var next = NextSessionsAnalysis.Of(new NextInputs(
            Today,
            built,
            PriorityAnalysis.Read(built, [], Today),
            TrainingScoreAnalysis.Score(built, [], _ => new CardioMeasure.Unknown(CardioUnknownReason.NoHeartRate), Today),
            [],
            Rested));

        Assert.IsType<NextCardio.Zone2>(next.Cardio);
        Assert.Contains(next.First.Reasons, reason => reason.Contains("could not be read"));
    }

    [Fact]
    public void Each_choice_comes_with_its_reasons()
    {
        var next = Next(AWeek());

        Assert.NotEmpty(next.First.Reasons);
        Assert.NotEmpty(next.Lift.Reasons);
        Assert.NotEmpty(next.Cardio.Reasons);
    }

    /// <summary>A run with twelve minutes of work bouts on the day.</summary>
    private static CardioSession Intervals(DateOnly day)
    {
        var start = new DateTimeOffset(day.ToDateTime(new TimeOnly(18, 0)), TimeSpan.FromHours(-5));
        return new CardioSession(
            new CardioSessionSource.LoggedSession(Guid.NewGuid(), true, true), start, start.AddMinutes(40), 8000,
            new SessionHeartRate.Recorded(160, 185, 1, new ZoneMinutes(0, 5, 10, 10, 12, 3), HeartRateSource.RecordingApp)
            {
                Bouts = [new WorkBout(start.AddMinutes(10), 6, 172), new WorkBout(start.AddMinutes(20), 6, 174)],
            });
    }
}
