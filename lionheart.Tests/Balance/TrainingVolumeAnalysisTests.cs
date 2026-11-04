using lionheart.Model.Training.Balance;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Sets;
using lionheart.Services.Training.Balance;
using static lionheart.Tests.Balance.ALog;

namespace lionheart.Tests.Balance;

/// <summary>
/// Weekly volume: which sets count, how muscles are paid, which seven days the window holds, and
/// what the log says is up next.
/// </summary>
public class TrainingVolumeAnalysisTests
{
    private static readonly DateOnly Wednesday = new(2026, 11, 4);
    private static readonly ReadinessNote NoReadiness = new ReadinessNote.NotRead();

    private static TrainingVolume Volume(ALog log, DateOnly? endingOn = null) =>
        TrainingVolumeAnalysis.Build(log.Build(), endingOn ?? Wednesday, NoReadiness);

    private static FamilyVolume Family(TrainingVolume volume, PatternFamily family) =>
        volume.Families.Single(entry => entry.Family == family);

    [Fact]
    public void Working_drop_and_failure_sets_are_hard_and_a_warm_up_is_not()
    {
        var volume = Volume(Catalogue.WithSession(Wednesday,
            Set(Bench, kind: SetKind.WarmUp), Set(Bench), Set(Bench, kind: SetKind.Drop), Set(Bench, kind: SetKind.Failure)));

        Assert.Equal(3, Family(volume, PatternFamily.Push).HardSets);
    }

    [Fact]
    public void The_window_is_the_seven_days_ending_on_the_day_asked_for()
    {
        var volume = Volume(Catalogue
            .WithSession(Wednesday.AddDays(-7), Set(Squat))
            .WithSession(Wednesday.AddDays(-6), Set(Squat))
            .WithSession(Wednesday, Set(Squat))
            .WithSession(Wednesday.AddDays(1), Set(Squat)));

        Assert.Equal((Wednesday.AddDays(-6), Wednesday), (volume.Window.From, volume.Window.To));
        Assert.Equal(2, Family(volume, PatternFamily.Legs).HardSets);
        Assert.Equal(Wednesday, Family(volume, PatternFamily.Legs).LastTrained);
    }

    [Fact]
    public void A_primary_muscle_is_paid_a_whole_set_and_a_secondary_one_half()
    {
        var volume = Volume(Catalogue.WithSession(Wednesday, Sets(2, Bench)));

        var triceps = volume.Muscles.Single(muscle => muscle.Muscle == MuscleGroup.Triceps);
        var chest = volume.Muscles.Single(muscle => muscle.Muscle == MuscleGroup.Chest);
        Assert.Equal((0, 2, 1.0), (triceps.Direct, triceps.Indirect, triceps.Sets));
        Assert.Equal((2, 0, 2.0), (chest.Direct, chest.Indirect, chest.Sets));
    }

    [Fact]
    public void An_isolation_movement_counts_toward_its_muscles_family_and_not_as_a_compound_pattern()
    {
        var volume = Volume(Catalogue.WithSession(Wednesday, Sets(3, Curl)));

        Assert.Equal(3, Family(volume, PatternFamily.Pull).HardSets);
        Assert.Equal(0, volume.Patterns.Single(pattern => pattern.Pattern == MovementPattern.HorizontalPull).HardSets);
        var isolation = volume.Patterns.Single(pattern => pattern.Pattern == MovementPattern.Isolation);
        Assert.Equal((3, (int?)null), (isolation.HardSets, isolation.Target));
    }

    [Fact]
    public void Sets_of_an_unclassified_movement_are_listed_as_unplaced_and_counted_nowhere()
    {
        var volume = Volume(Catalogue.WithSession(Wednesday, Sets(2, Mystery)));

        Assert.Equal([new UnplacedMovement(Mystery, "Cable Mystery", 2)], volume.Unplaced);
        Assert.All(volume.Families, family => Assert.Equal(0, family.HardSets));
    }

    [Fact]
    public void After_push_on_monday_and_legs_on_tuesday_pull_is_up_next_on_wednesday()
    {
        var volume = Volume(Catalogue
            .WithSession(Wednesday.AddDays(-5), Set(Row))
            .WithSession(Wednesday.AddDays(-2), Set(Bench))
            .WithSession(Wednesday.AddDays(-1), Set(Squat)));

        var next = Assert.IsType<UpNext.Train>(volume.UpNext);
        Assert.Equal([PatternFamily.Pull], next.Families);
        Assert.Equal(Wednesday.AddDays(-5), next.LastTrained);
        Assert.False(next.TrainedToday);
    }

    [Fact]
    public void Two_accessory_sets_on_a_push_day_do_not_make_it_a_pull_day()
    {
        var volume = Volume(Catalogue
            .WithSession(Wednesday.AddDays(-5), Set(Row))
            .WithSession(Wednesday.AddDays(-2), Set(Bench), Set(Bench), Set(Curl), Set(Curl))
            .WithSession(Wednesday.AddDays(-1), Set(Squat)));

        Assert.Equal([PatternFamily.Pull], Assert.IsType<UpNext.Train>(volume.UpNext).Families);
        Assert.Equal(Wednesday.AddDays(-5), Family(volume, PatternFamily.Pull).LastTrained);
        Assert.Equal(3, Family(volume, PatternFamily.Pull).HardSets);
    }

    [Fact]
    public void A_day_of_three_accessory_sets_trains_their_family()
    {
        var volume = Volume(Catalogue.WithSession(Wednesday.AddDays(-1), Set(Curl), Set(Curl), Set(Curl)));

        Assert.Equal(Wednesday.AddDays(-1), Family(volume, PatternFamily.Pull).LastTrained);
    }

    [Fact]
    public void Families_last_trained_the_same_day_are_up_next_together()
    {
        var volume = Volume(Catalogue
            .WithSession(Wednesday.AddDays(-2), Set(Bench), Set(Row))
            .WithSession(Wednesday.AddDays(-1), Set(Squat)));

        Assert.Equal([PatternFamily.Push, PatternFamily.Pull], Assert.IsType<UpNext.Train>(volume.UpNext).Families);
    }

    [Fact]
    public void A_family_never_trained_is_up_next_before_any_trained_one()
    {
        var volume = Volume(Catalogue
            .WithSession(Wednesday.AddDays(-30), Set(Bench))
            .WithSession(Wednesday.AddDays(-1), Set(Squat)));

        var next = Assert.IsType<UpNext.Train>(volume.UpNext);
        Assert.Equal([PatternFamily.Pull], next.Families);
        Assert.Null(next.LastTrained);
    }

    [Fact]
    public void A_session_today_makes_the_recommendation_the_next_session()
    {
        var volume = Volume(Catalogue.WithSession(Wednesday, Set(Bench), Set(Row)));

        var next = Assert.IsType<UpNext.Train>(volume.UpNext);
        Assert.True(next.TrainedToday);
        Assert.Equal([PatternFamily.Legs], next.Families);
    }

    [Fact]
    public void With_no_classified_lifting_there_is_nothing_to_order() =>
        Assert.IsType<UpNext.NothingLogged>(Volume(Catalogue.WithSession(Wednesday, Set(Mystery))).UpNext);

    [Fact]
    public void Readiness_under_the_tenth_percentile_of_ninety_days_is_low_and_says_the_range()
    {
        var usual = Enumerable.Range(1, 60).Select(days => (Wednesday.AddDays(-days), 70 + days % 20)).ToList();

        var low = Assert.IsType<ReadinessNote.Low>(ReadinessNotes.Of([.. usual, (Wednesday, 60)], Wednesday));
        Assert.Equal(60, low.OuraScore);
        Assert.True(low.UsualLow > 60 && low.UsualHigh > low.UsualLow);
        Assert.IsType<ReadinessNote.Usual>(ReadinessNotes.Of([.. usual, (Wednesday, 80)], Wednesday));
    }

    [Fact]
    public void Readiness_with_under_thirty_days_of_history_is_not_read()
    {
        var short_ = Enumerable.Range(1, 29).Select(days => (Wednesday.AddDays(-days), 80)).ToList();

        Assert.IsType<ReadinessNote.NotRead>(ReadinessNotes.Of([.. short_, (Wednesday, 40)], Wednesday));
        Assert.IsType<ReadinessNote.NotRead>(ReadinessNotes.Of(short_, Wednesday));
    }
}
