using lionheart.Model.Analysis;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Tests.Support;

namespace lionheart.Tests.Domain;

/// <summary>
/// The revision rule: which corrections reach the records, and what a set left alone keeps.
/// </summary>
public class SessionRevisionTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 3, 17, 0, 0, TimeSpan.FromHours(-4));
    private static readonly Guid Bench = Guid.NewGuid();

    [Fact]
    public void A_notes_only_correction_does_not_reach_the_records_and_keeps_the_decoration()
    {
        var current = Session([Lift(225)]) with { Decoration = new Decorated(Some.Decoration) };

        var revision = SessionRevision.Of(current, [Lift(225)], current.Period, "new notes");

        Assert.False(revision.AffectsRecords);
        Assert.IsType<Decorated>(revision.Revised.Decoration);
        Assert.Equal("new notes", revision.Revised.Notes);
    }

    [Fact]
    public void A_changed_set_reaches_the_records_and_drops_the_decoration_that_described_the_old_ones()
    {
        var current = Session([Lift(225)]) with { Decoration = new Decorated(Some.Decoration) };

        var revision = SessionRevision.Of(current, [Lift(235)], current.Period, current.Notes);

        Assert.True(revision.AffectsRecords);
        Assert.IsType<NotDecorated>(revision.Revised.Decoration);
    }

    [Fact]
    public void A_removed_set_reaches_the_records()
    {
        var current = Session([Lift(225), Lift(235)]);

        Assert.True(SessionRevision.Of(current, [Lift(225)], current.Period, current.Notes).AffectsRecords);
    }

    [Fact]
    public void Moving_the_start_reaches_the_records_but_moving_only_the_end_does_not()
    {
        // A record is dated by its session's start; the end is the session's length and nothing more.
        var current = Session([Lift(225)]);

        Assert.True(SessionRevision.Of(
            current, [Lift(225)], new Interval(Start.AddDays(-1), Start.AddDays(-1).AddHours(1)), "").AffectsRecords);
        Assert.False(SessionRevision.Of(
            current, [Lift(225)], new Interval(Start, Start.AddHours(2)), "").AffectsRecords);
    }

    [Fact]
    public void The_same_moment_at_another_offset_is_not_a_move()
    {
        var current = Session([Lift(225)]);
        var sameInstant = Start.ToOffset(TimeSpan.Zero);

        Assert.False(SessionRevision.Of(
            current, [Lift(225)], new Interval(sameInstant, sameInstant.AddHours(1)), "").AffectsRecords);
    }

    [Fact]
    public void A_texted_set_left_as_it_was_keeps_the_words_it_was_logged_in()
    {
        var texted = Lift(225) with { Source = Some.TextedSource, Facets = Some.Facets };
        var current = Session([texted, Lift(235)]);

        var revision = SessionRevision.Of(current, [Lift(225), Lift(245)], current.Period, current.Notes);

        Assert.Equal(Some.TextedSource, revision.Revised.Sets[0].Source);
        Assert.Same(texted, revision.Revised.Sets[0]);
        Assert.IsType<Formed>(revision.Revised.Sets[1].Source);
    }

    [Fact]
    public void Two_identical_sets_each_keep_their_own_source()
    {
        var spoken = Lift(225) with { Source = new Texted("225 for 5", 0.9) };
        var typed = Lift(225);
        var current = Session([spoken, typed]);

        var revision = SessionRevision.Of(current, [Lift(225), Lift(225)], current.Period, "");

        Assert.False(revision.AffectsRecords);
        Assert.Same(spoken, revision.Revised.Sets[0]);
        Assert.Same(typed, revision.Revised.Sets[1]);
    }

    [Fact]
    public void A_freeform_set_is_the_same_set_while_its_words_are()
    {
        var current = Session([Freeform("hip mobility")]);

        Assert.False(SessionRevision.Of(current, [Freeform("hip mobility")], current.Period, "").AffectsRecords);
        Assert.True(SessionRevision.Of(current, [Freeform("ankle mobility")], current.Period, "").AffectsRecords);
    }

    private static LiftSet Lift(double weight) =>
        new(Bench, 5, weight, WeightUnit.Pounds, Rpe.Unrated, SetKind.Working) { Source = new Formed(), Facets = [] };

    private static FreeformSet Freeform(string words) => new() { Source = new Texted(words, 0), Facets = [] };

    private static CompletedTrainingSession Session(IReadOnlyList<SetEntry> sets) =>
        Some.CompletedSession(Guid.NewGuid()) with
        {
            Sets = sets,
            Period = new Interval(Start, Start.AddHours(1)),
            Notes = "",
        };
}
