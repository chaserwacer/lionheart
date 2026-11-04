using lionheart.Functional;
using lionheart.Model.BodyMeasurements;
using lionheart.Model.Health;
using lionheart.Model.Health.Measurements;
using lionheart.Model.Training.Sets;
using lionheart.Services.Health.Measurements;
using lionheart.Services.Training.Balance;
using lionheart.Tests.Support;

namespace lionheart.Tests.Health;

/// <summary>
/// The pure rules behind Apple Health's measurements: what a day of readings can be, which
/// weigh-ins become bodyweights and in what unit, which sets a backfill re-reads, and an age from a
/// date of birth. All values are invented.
/// </summary>
public class HealthMeasureRuleTests
{
    private static readonly DateOnly Day = new(2021, 3, 14);

    // ---- A day of readings ----

    [Fact]
    public void A_day_of_readings_within_the_figures_bounds_is_kept_as_sent()
    {
        var aggregate = ResultAssert.Succeeded(DayAggregate.Of(HealthMetric.HeartRateVariabilitySdnn, 48, 22, 91, 7));

        Assert.Equal((48.0, 22.0, 91.0, 7), (aggregate.Mean, aggregate.Minimum, aggregate.Maximum, aggregate.Count));
    }

    [Theory]
    [InlineData(HealthMetric.WalkingAsymmetry, 0.04, 0.0, 0.1, 0, "Count")]
    [InlineData(HealthMetric.WalkingSpeed, 1.6, 1.7, 1.8, 3, "Mean")]
    [InlineData(HealthMetric.WalkingAsymmetry, 4, 2, 6, 3, "Mean")]
    [InlineData(HealthMetric.HeartRateVariabilitySdnn, 0.05, 0.04, 0.06, 2, "Mean")]
    [InlineData(HealthMetric.RestingHeartRate, double.NaN, 50, 60, 1, "Mean")]
    public void A_day_that_cannot_be_readings_of_its_figure_is_refused(
        HealthMetric metric, double mean, double minimum, double maximum, int count, string field)
    {
        var problem = Assert.IsType<Problem.Invalid>(ResultAssert.Failed(DayAggregate.Of(metric, mean, minimum, maximum, count)));

        Assert.Equal(field, Assert.Single(problem.Fields).Field);
    }

    [Fact]
    public void Every_figure_has_bounds()
    {
        foreach (var metric in Enum.GetValues<HealthMetric>())
        {
            var (lowest, highest) = HealthMetricBounds.Of(metric);
            Assert.True(lowest < highest, $"{metric}");
        }
    }

    // ---- Weigh-ins ----

    [Fact]
    public void A_day_weighed_twice_takes_its_first_weigh_in()
    {
        var plan = WeighInPlan.Of(
            [WeighIn(Day, hour: 19, kilograms: 81.0), WeighIn(Day, hour: 7, kilograms: 80.2)],
            new HashSet<DateOnly>(),
            WeightUnit.Kilograms);

        Assert.Equal([new BodyweightOnDay(Day, new Bodyweight(80.2, WeightUnit.Kilograms))], plan.Added);
    }

    [Fact]
    public void A_day_already_measured_in_Lionheart_is_kept_and_counted()
    {
        var other = Day.AddDays(1);

        var plan = WeighInPlan.Of(
            [WeighIn(Day, 7, 80.0), WeighIn(Day, 8, 80.1), WeighIn(other, 7, 79.8)],
            new HashSet<DateOnly> { Day },
            WeightUnit.Kilograms);

        Assert.Equal([other], plan.Added.Select(added => added.Day));
        Assert.Equal(1, plan.DaysAlreadyMeasured);
    }

    [Fact]
    public void A_weigh_in_is_stated_in_pounds_to_a_tenth_for_an_athlete_who_weighs_in_pounds()
    {
        var plan = WeighInPlan.Of([WeighIn(Day, 7, 80)], new HashSet<DateOnly>(), WeightUnit.Pounds);

        Assert.Equal(new Bodyweight(176.4, WeightUnit.Pounds), Assert.Single(plan.Added).Bodyweight);
    }

    [Fact]
    public void A_weigh_in_that_cannot_be_a_persons_weight_is_left_out_and_counted()
    {
        var plan = WeighInPlan.Of(
            [WeighIn(Day, 7, 0), WeighIn(Day.AddDays(1), 7, double.NaN), WeighIn(Day.AddDays(2), 7, 79)],
            new HashSet<DateOnly>(),
            WeightUnit.Kilograms);

        Assert.Equal((1, 2), (plan.Added.Count, plan.Unreadable));
    }

    [Fact]
    public void The_unit_is_the_newest_measurements_else_the_one_most_lifts_are_logged_in_else_kilograms()
    {
        var newest = new BodyweightOnDay(Day, new Bodyweight(170, WeightUnit.Pounds));

        Assert.Equal(WeightUnit.Pounds, WeighInPlan.UnitFor(newest, [WeightUnit.Kilograms]));
        Assert.Equal(WeightUnit.Pounds, WeighInPlan.UnitFor(null, [WeightUnit.Pounds, WeightUnit.Kilograms, WeightUnit.Pounds]));
        Assert.Equal(WeightUnit.Kilograms, WeighInPlan.UnitFor(null, []));
    }

    // ---- Sets a backfill re-reads ----

    [Fact]
    public void A_set_counts_as_reweighed_when_the_bodyweight_it_reads_changes()
    {
        var scan = new BodyweightOnDay(new DateOnly(2025, 6, 1), new Bodyweight(180, WeightUnit.Pounds));
        var older = new BodyweightOnDay(new DateOnly(2020, 1, 10), new Bodyweight(172, WeightUnit.Pounds));
        IReadOnlyList<(DateOnly, WeightUnit)> sets =
        [
            // Before any measurement the scan stood in; now the 2020 weigh-in does.
            (new DateOnly(2020, 2, 1), WeightUnit.Pounds),
            // After the scan: the scan both times.
            (new DateOnly(2025, 7, 1), WeightUnit.Pounds),
            // Kilograms: no bodyweight in that unit either time.
            (new DateOnly(2020, 2, 1), WeightUnit.Kilograms),
        ];

        Assert.Equal(1, WeighInPlan.Reweighed(sets, [scan], [older, scan]));
    }

    [Fact]
    public void The_bodyweight_on_a_day_before_any_measurement_is_the_first_one_in_the_unit()
    {
        var first = new BodyweightOnDay(new DateOnly(2020, 1, 10), new Bodyweight(172, WeightUnit.Pounds));
        var later = new BodyweightOnDay(new DateOnly(2021, 1, 10), new Bodyweight(176, WeightUnit.Pounds));

        Assert.Equal(first.Bodyweight, TrainingLog.BodyweightOn([first, later], new DateOnly(2019, 5, 1), WeightUnit.Pounds));
        Assert.Equal(first.Bodyweight, TrainingLog.BodyweightOn([first, later], new DateOnly(2020, 12, 31), WeightUnit.Pounds));
        Assert.Null(TrainingLog.BodyweightOn([first, later], new DateOnly(2020, 12, 31), WeightUnit.Kilograms));
    }

    // ---- Date of birth ----

    [Theory]
    [InlineData("2026-05-19", 35)]
    [InlineData("2026-05-20", 36)]
    [InlineData("2026-11-04", 36)]
    public void An_age_turns_over_on_the_birthday(string today, int age)
    {
        var birth = new HealthBirthDate(new DateOnly(1990, 5, 20));

        Assert.Equal(age, ResultAssert.Succeeded(birth.AgeOn(DateOnly.Parse(today))));
    }

    [Fact]
    public void A_leap_day_birthday_turns_over_on_the_28th_in_other_years()
    {
        var birth = new HealthBirthDate(new DateOnly(1992, 2, 29));

        Assert.Equal(33, ResultAssert.Succeeded(birth.AgeOn(new DateOnly(2026, 2, 27))));
        Assert.Equal(34, ResultAssert.Succeeded(birth.AgeOn(new DateOnly(2026, 2, 28))));
    }

    [Theory]
    [InlineData("2027-01-01")]
    [InlineData("1890-01-01")]
    public void A_date_that_cannot_be_a_living_persons_birth_is_refused(string born)
    {
        Assert.IsType<Problem.Invalid>(ResultAssert.Failed(new HealthBirthDate(DateOnly.Parse(born)).AgeOn(new DateOnly(2026, 11, 4))));
    }

    private static WeighInCommand WeighIn(DateOnly day, int hour, double kilograms) =>
        new(day, new DateTimeOffset(day.ToDateTime(new TimeOnly(hour, 0)), TimeSpan.FromHours(-5)), kilograms);
}
