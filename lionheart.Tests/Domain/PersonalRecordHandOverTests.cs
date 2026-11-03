using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Sets;
using lionheart.Tests.Support;

namespace lionheart.Tests.Domain;

/// <summary>
/// The aggregate's own guards, which are where the invariant lives.
/// </summary>
/// <remarks>
/// No relational constraint can carry "at most one record of an identity stands": SQLite checks a
/// unique index one statement at a time, so an index over the standing records refuses every
/// hand-over — the insert lands while the row it replaces is still standing. These factories and
/// transitions are therefore the only line of defence, and they refuse rather than trust. Every
/// caller is inside this assembly, so a hand-over that does not hold is a defect here and not an
/// answer for an athlete, which is why these throw rather than returning a problem.
/// </remarks>
public class PersonalRecordHandOverTests
{
    private static readonly Guid Athlete = Guid.NewGuid();
    private static readonly RecordSubject Bench = new RecordSubject.Movement(Guid.NewGuid());

    [Fact]
    public void A_hand_over_demotes_the_record_it_takes_from_and_points_back_at_it()
    {
        var first = Standing(Heaviest(225, WeightUnit.Pounds));

        var second = PersonalRecord.Succeeding(
            Guid.CreateVersion7(), Heaviest(245, WeightUnit.Pounds), Some.Clock.AddDays(7), first);

        Assert.IsType<RecordStanding.Beaten>(first.Standing);
        Assert.Equal(Some.Clock.AddDays(7), ((RecordStanding.Beaten)first.Standing).At);
        Assert.True(second.IsStanding);
        Assert.Equal(first.PersonalRecordId, ((RecordLineage.Replaced)second.Lineage).PersonalRecordId);
        Assert.Equal(first.AchievedAt, ((RecordLineage.Replaced)second.Lineage).AchievedAt);
        Assert.Equal(first.Key, second.Key);
    }

    [Fact]
    public void A_record_cannot_succeed_one_it_does_not_beat()
    {
        var standing = Standing(Heaviest(225, WeightUnit.Pounds));

        Assert.Throws<InvalidOperationException>(() => PersonalRecord.Succeeding(
            Guid.CreateVersion7(), Heaviest(205, WeightUnit.Pounds), Some.Clock.AddDays(7), standing));
    }

    [Fact]
    public void Equalling_the_standing_record_does_not_take_it()
    {
        var standing = Standing(Heaviest(225, WeightUnit.Pounds));

        Assert.Throws<InvalidOperationException>(() => PersonalRecord.Succeeding(
            Guid.CreateVersion7(), Heaviest(225, WeightUnit.Pounds), Some.Clock.AddDays(7), standing));
    }

    [Fact]
    public void A_record_cannot_succeed_one_of_another_kind()
    {
        // Letting this through is how a movement ends up with a best-set-volume record standing in
        // the heaviest-set chain, which no read could then make sense of.
        var standing = Standing(Heaviest(225, WeightUnit.Pounds));
        var volume = new Achievement.BestSetVolume(new LiftPerformance(300, WeightUnit.Pounds, 10));

        Assert.Throws<InvalidOperationException>(() => PersonalRecord.Succeeding(
            Guid.CreateVersion7(), volume, Some.Clock.AddDays(7), standing));
    }

    [Fact]
    public void A_record_cannot_succeed_one_held_in_another_unit()
    {
        // 100 kg is the heavier lift and nothing converts, so it is not a better pounds record —
        // it is a different record, and it sets its own.
        var standing = Standing(Heaviest(225, WeightUnit.Pounds));

        Assert.Throws<InvalidOperationException>(() => PersonalRecord.Succeeding(
            Guid.CreateVersion7(), Heaviest(100, WeightUnit.Kilograms), Some.Clock.AddDays(7), standing));
    }

    [Fact]
    public void A_record_that_no_longer_stands_cannot_be_succeeded_again()
    {
        var first = Standing(Heaviest(225, WeightUnit.Pounds));
        PersonalRecord.Succeeding(
            Guid.CreateVersion7(), Heaviest(245, WeightUnit.Pounds), Some.Clock.AddDays(7), first);

        Assert.Throws<InvalidOperationException>(() => PersonalRecord.Succeeding(
            Guid.CreateVersion7(), Heaviest(255, WeightUnit.Pounds), Some.Clock.AddDays(14), first));
    }

    [Fact]
    public void Only_the_standing_record_is_retracted()
    {
        var first = Standing(Heaviest(225, WeightUnit.Pounds));
        PersonalRecord.Succeeding(
            Guid.CreateVersion7(), Heaviest(245, WeightUnit.Pounds), Some.Clock.AddDays(7), first);

        Assert.Throws<InvalidOperationException>(() => first.Retract(Some.Clock.AddDays(8)));
    }

    [Fact]
    public void A_withdrawn_record_is_not_withdrawn_twice()
    {
        var only = Standing(Heaviest(225, WeightUnit.Pounds));
        only.Retract(Some.Clock.AddDays(1));

        Assert.Throws<InvalidOperationException>(() => only.Retract(Some.Clock.AddDays(2)));
    }

    [Fact]
    public void Only_a_record_the_athlete_beat_comes_back()
    {
        // Restoring a withdrawn record would put back a number the athlete said was never real,
        // and restoring the standing one is a no-op hiding a bug in the caller.
        var standing = Standing(Heaviest(225, WeightUnit.Pounds));
        Assert.Throws<InvalidOperationException>(standing.Restore);

        var withdrawn = Standing(Heaviest(225, WeightUnit.Pounds));
        withdrawn.Retract(Some.Clock.AddDays(1));
        Assert.Throws<InvalidOperationException>(withdrawn.Restore);
    }

    [Fact]
    public void A_beaten_record_comes_back_standing()
    {
        var first = Standing(Heaviest(225, WeightUnit.Pounds));
        PersonalRecord.Succeeding(
            Guid.CreateVersion7(), Heaviest(245, WeightUnit.Pounds), Some.Clock.AddDays(7), first);

        first.Restore();

        Assert.True(first.IsStanding);
    }

    [Fact]
    public void A_records_identity_carries_the_unit_it_is_held_in()
    {
        var inPounds = Standing(Heaviest(225, WeightUnit.Pounds));
        var inKilograms = Standing(Heaviest(100, WeightUnit.Kilograms));

        Assert.NotEqual(inPounds.Key, inKilograms.Key);
        Assert.Equal(WeightUnit.Pounds, inPounds.Key.HeldIn);
        Assert.Equal(WeightUnit.Kilograms, inKilograms.Key.HeldIn);
    }

    [Fact]
    public void A_distance_records_identity_carries_no_unit()
    {
        // Distances rank in meters, so one record covers every spelling of a distance.
        var record = Standing(new Achievement.LongestDistance(
            new DistanceEffort(5, DistanceUnit.Kilometers, TimeSpan.FromMinutes(25))));

        Assert.Null(record.Key.HeldIn);
    }

    private static PersonalRecord Standing(Achievement achievement) =>
        PersonalRecord.FirstOf(Guid.CreateVersion7(), Athlete, Bench, achievement, Some.Clock);

    private static Achievement Heaviest(double weight, WeightUnit unit) =>
        new Achievement.HeaviestSet(new LiftPerformance(weight, unit, Reps: 5));
}
