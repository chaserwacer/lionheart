using lionheart.Functional;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Sets;
using lionheart.Tests.Support;

namespace lionheart.Tests.Domain;

/// <summary>
/// What a record is the best of, how two of them are ranked, and the figures read off a stored one
/// rather than stored beside it.
/// </summary>
public class AchievementTests
{
    [Fact]
    public void A_single_implies_itself_rather_than_Epleys_three_percent_bonus()
    {
        // Epley answers weight × (1 + reps/30), which at one rep is 1.033 × the weight — a single
        // at 225 would report an estimated max of 232.
        var single = new LiftPerformance(225, WeightUnit.Pounds, Reps: 1);

        Assert.Equal(225, single.EstimatedOneRepMax);
    }

    [Fact]
    public void A_five_rep_set_estimates_a_single_by_Epley()
    {
        var fiver = new LiftPerformance(225, WeightUnit.Pounds, Reps: 5);

        Assert.Equal(225 * (1 + 5 / 30.0), fiver.EstimatedOneRepMax, precision: 6);
    }

    [Fact]
    public void A_sets_volume_is_its_weight_times_its_reps() =>
        Assert.Equal(1125, new LiftPerformance(225, WeightUnit.Pounds, 5).Volume);

    [Fact]
    public void A_heavier_set_beats_a_lighter_one_in_the_same_unit()
    {
        var lighter = Heaviest(225, WeightUnit.Pounds, 5);
        var heavier = Heaviest(230, WeightUnit.Pounds, 3);

        Assert.True(heavier.Beats(lighter));
        Assert.False(lighter.Beats(heavier));
    }

    [Fact]
    public void Equalling_a_record_does_not_take_it()
    {
        // Which is also what keeps a repeated working set from announcing itself every time.
        var standing = Heaviest(225, WeightUnit.Pounds, 5);

        Assert.False(Heaviest(225, WeightUnit.Pounds, 5).Beats(standing));
    }

    [Fact]
    public void A_bigger_number_in_the_other_unit_does_not_beat_the_record()
    {
        // 225 lb is the larger number and the lighter lift. Nothing here converts, so the honest
        // answer is that the two were never compared — which is what both clients also do.
        var inKilograms = Heaviest(100, WeightUnit.Kilograms, 5);

        Assert.False(Heaviest(225, WeightUnit.Pounds, 5).Beats(inKilograms));
        Assert.False(inKilograms.Beats(Heaviest(225, WeightUnit.Pounds, 5)));
    }

    [Fact]
    public void A_record_of_one_kind_never_beats_a_record_of_another()
    {
        var volume = new Achievement.BestSetVolume(new LiftPerformance(100, WeightUnit.Pounds, 20));

        Assert.False(volume.Beats(Heaviest(225, WeightUnit.Pounds, 1)));
    }

    [Fact]
    public void Two_rep_maxes_at_different_rep_counts_are_different_records()
    {
        var threeRepMax = new Achievement.RepMax(new LiftPerformance(255, WeightUnit.Pounds, 3));
        var fiveRepMax = new Achievement.RepMax(new LiftPerformance(225, WeightUnit.Pounds, 5));

        Assert.NotEqual(threeRepMax.Kind, fiveRepMax.Kind);
        Assert.False(threeRepMax.Beats(fiveRepMax));
    }

    [Fact]
    public void Distances_rank_across_units_because_a_kilometre_is_a_thousand_metres()
    {
        // Unlike weights: the four distance units are exact definitions of one quantity, and two
        // of them are metric spellings of the same scale.
        var fiveKilometers = Longest(5, DistanceUnit.Kilometers, TimeSpan.FromMinutes(25));
        var threeMiles = Longest(3, DistanceUnit.Miles, TimeSpan.FromMinutes(24));

        Assert.True(fiveKilometers.Beats(threeMiles));
    }

    [Fact]
    public void Five_kilometers_and_five_thousand_meters_are_the_same_fastest_time_bracket()
    {
        var metric = new Achievement.FastestTime(
            new DistanceEffort(5, DistanceUnit.Kilometers, TimeSpan.FromMinutes(22)));
        var spelledOut = new Achievement.FastestTime(
            new DistanceEffort(5000, DistanceUnit.Meters, TimeSpan.FromMinutes(21)));

        Assert.Equal(metric.Kind, spelledOut.Kind);
        Assert.True(spelledOut.Beats(metric));
    }

    [Fact]
    public void A_different_distance_is_a_different_bracket_rather_than_a_faster_time()
    {
        // A ten-kilometre run carries no five-kilometre split, so crediting one from its total
        // time would report a time the athlete never ran.
        var fiveK = new Achievement.FastestTime(
            new DistanceEffort(5, DistanceUnit.Kilometers, TimeSpan.FromMinutes(22)));
        var tenK = new Achievement.FastestTime(
            new DistanceEffort(10, DistanceUnit.Kilometers, TimeSpan.FromMinutes(48)));

        Assert.NotEqual(fiveK.Kind, tenK.Kind);
    }

    [Fact]
    public void A_pace_is_read_off_the_distance_and_the_time_rather_than_stored()
    {
        var effort = new DistanceEffort(5, DistanceUnit.Kilometers, TimeSpan.FromMinutes(25));

        Assert.Equal(TimeSpan.FromMinutes(5), effort.PacePerKilometer);
        Assert.Equal(
            TimeSpan.FromSeconds(25 * 60 / 5 * 1.609344).TotalSeconds,
            effort.PacePerMile.TotalSeconds,
            precision: 6);
    }

    [Fact]
    public void An_effort_over_no_distance_ranks_last_rather_than_throwing()
    {
        // Totality at the storage boundary: one corrupt row must not make every record on the
        // movement unreadable.
        var nothing = new DistanceEffort(0, DistanceUnit.Kilometers, TimeSpan.FromMinutes(25));

        Assert.Equal(double.PositiveInfinity, nothing.SecondsPerMeter);
        Assert.Equal(TimeSpan.Zero, nothing.PacePerKilometer);
    }

    [Theory]
    [InlineData("heaviestSet")]
    [InlineData("bestSetVolume")]
    [InlineData("bestSessionVolume")]
    [InlineData("longestDistance")]
    [InlineData("bestPace")]
    public void An_unbracketed_kind_is_named_without_one(string name)
    {
        var kind = ResultAssert.Succeeded(RecordKind.Of(name, bracket: null));

        Assert.Equal(name, kind.Name);
        Assert.Equal(0, kind.Bracket);
    }

    [Fact]
    public void An_unbracketed_kind_given_a_bracket_is_refused()
    {
        var problem = ResultAssert.Failed(RecordKind.Of("heaviestSet", bracket: 5));

        Assert.IsType<Problem.Invalid>(problem);
    }

    [Fact]
    public void A_rep_max_without_its_rep_count_is_refused()
    {
        // Asking for "my rep max" without saying at how many reps is a question with no answer,
        // not a question about one rep.
        Assert.IsType<Problem.Invalid>(ResultAssert.Failed(RecordKind.Of("repMax", bracket: null)));
        Assert.IsType<Problem.Invalid>(ResultAssert.Failed(RecordKind.Of("repMax", bracket: 0)));
    }

    [Fact]
    public void A_kind_nobody_defined_is_refused_rather_than_guessed() =>
        Assert.IsType<Problem.Invalid>(ResultAssert.Failed(RecordKind.Of("mostSetsEver", null)));

    [Fact]
    public void A_named_kind_round_trips_through_its_own_spelling_and_bracket()
    {
        var kinds = new RecordKind[]
        {
            new RecordKind.HeaviestSet(),
            new RecordKind.BestSetVolume(),
            new RecordKind.BestSessionVolume(),
            new RecordKind.LongestDistance(),
            new RecordKind.BestPace(),
            new RecordKind.RepMax(5),
            new RecordKind.FastestTime(5000),
        };

        foreach (var kind in kinds)
        {
            var named = ResultAssert.Succeeded(
                RecordKind.Of(kind.Name, kind.Bracket == 0 ? null : kind.Bracket));

            Assert.Equal(kind, named);
        }
    }

    /// <summary>
    /// Every achievement has a view. A switch with a discard arm compiles no matter how many cases
    /// it forgot, so until the language checks exhaustiveness this test is the only thing watching.
    /// </summary>
    [Fact]
    public void Every_kind_of_achievement_projects_to_its_own_view()
    {
        var lift = new LiftPerformance(225, WeightUnit.Pounds, 5);
        var effort = new DistanceEffort(5, DistanceUnit.Kilometers, TimeSpan.FromMinutes(22));

        var views = new Achievement[]
        {
            new Achievement.HeaviestSet(lift),
            new Achievement.BestSetVolume(lift),
            new Achievement.RepMax(lift),
            new Achievement.BestSessionVolume(new SessionVolume(12400, WeightUnit.Pounds)),
            new Achievement.LongestDistance(effort),
            new Achievement.FastestTime(effort),
            new Achievement.BestPace(effort),
        }.Select(AchievementView.Of).ToList();

        Assert.Equal(7, views.Select(view => view.GetType()).Distinct().Count());
    }

    private static Achievement Heaviest(double weight, WeightUnit unit, int reps) =>
        new Achievement.HeaviestSet(new LiftPerformance(weight, unit, reps));

    private static Achievement Longest(double distance, DistanceUnit unit, TimeSpan duration) =>
        new Achievement.LongestDistance(new DistanceEffort(distance, unit, duration));
}
