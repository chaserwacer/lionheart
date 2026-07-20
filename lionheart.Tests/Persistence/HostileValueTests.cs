using lionheart.Model.Analysis;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Persistence;

/// <summary>
/// Real logs are not tidy: emoji, pasted newlines, apostrophes and quotes, empty annotation
/// lists, and the odd non-finite number arriving from a parser. These go all the way through
/// SQLite and back.
/// </summary>
public sealed class HostileValueTests : IDisposable
{
    private const string Nasty = "PR! 💪🏋️\r\nquote:\" apostrophe:' backslash:\\ semi;colon -- DROP TABLE Sessions; 中文 \0end";

    private readonly RemodelDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Hostile_text_survives_notes_raw_text_and_summary()
    {
        var session = Some.CompletedSession(_db.UserId) with
        {
            Notes = Nasty,
            Sets = [Some.FreeformSet with { Source = new Texted(Nasty, 0.5) }],
            Decoration = new Decorated(Some.Decoration with { SummaryText = Nasty, FreeTags = [Nasty] }),
        };

        using (var context = _db.CreateContext())
        {
            context.Sessions.Add(session);
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        var loaded = (CompletedTrainingSession)fresh.Sessions.Single();

        Assert.Equal(Nasty, loaded.Notes);
        Assert.Equal(Nasty, Assert.IsType<Texted>(Assert.Single(loaded.Sets).Source).RawText);
        var decoration = Assert.IsType<Decorated>(loaded.Decoration).Decoration;
        Assert.Equal(Nasty, decoration.SummaryText);
        Assert.Equal(Nasty, Assert.Single(decoration.FreeTags));
    }

    [Fact]
    public void Hostile_text_survives_a_facet_value_and_a_program_title()
    {
        using (var context = _db.CreateContext())
        {
            context.FacetValues.Add(new FacetValue(FacetAxis.Intent, Nasty, [Nasty], Some.Embedding));
            context.Programs.Add(Some.ActiveProgram(_db.UserId) with { Title = Nasty, Tags = [Nasty] });
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        Assert.Equal(Nasty, fresh.FacetValues.Single().Canonical);
        Assert.Equal(Nasty, fresh.Programs.Single().Title);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1536)]
    public void Embeddings_round_trip_at_every_arity(int length)
    {
        var embedding = Enumerable.Range(0, length).Select(i => i * 0.001f).ToArray();

        using (var context = _db.CreateContext())
        {
            context.AnalyzableEntries.Add(Some.AnalyzableEntry(_db.UserId) with { Embedding = embedding });
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        Assert.Equal(embedding, fresh.AnalyzableEntries.Single().Embedding);
    }

    [Fact]
    public void Empty_annotation_lists_round_trip_as_empty_not_null()
    {
        var session = Some.CompletedSession(_db.UserId) with
        {
            Sets = [],
            Decoration = new Decorated(new Decoration([], [], Valence.Neutral, string.Empty, 1)),
        };

        using (var context = _db.CreateContext())
        {
            context.Sessions.Add(session);
            context.Contexts.Add(Some.StateContext(_db.UserId) with { Notes = string.Empty });
            context.AnalyzableEntries.Add(Some.AnalyzableEntry(_db.UserId) with { Facets = [] });
            context.Programs.Add(Some.ActiveProgram(_db.UserId) with { Tags = [] });
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        var loaded = (CompletedTrainingSession)fresh.Sessions.Single();
        Assert.Empty(loaded.Sets);
        var decoration = Assert.IsType<Decorated>(loaded.Decoration).Decoration;
        Assert.Empty(decoration.Facets);
        Assert.Empty(decoration.FreeTags);
        Assert.Empty(fresh.AnalyzableEntries.Single().Facets);
        Assert.Empty(fresh.Programs.Single().Tags);
    }

    [Fact]
    public void A_planned_session_with_no_prescriptions_round_trips()
    {
        var session = Some.PlannedSession(_db.UserId) with { Prescriptions = [] };

        using (var context = _db.CreateContext())
        {
            context.Sessions.Add(session);
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        Assert.Empty(((PlannedTrainingSession)fresh.Sessions.Single()).Prescriptions);
    }

    /// <summary>
    /// Refused at construction, where the value came from. Previously these built happily and
    /// surfaced much later as a System.Text.Json complaint raised during EF change tracking,
    /// which blamed a layer that had nothing to do with it.
    /// </summary>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Non_finite_measures_are_refused_when_the_set_is_built(double measure)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LiftSet(Guid.NewGuid(), Reps: 5, Weight: measure, WeightUnit.Pounds, Rpe.Of(8))
            {
                Source = new Formed(),
                Facets = [],
            });

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TrailRunSet(measure, DistanceUnit.Miles, TimeSpan.FromMinutes(64), ElevationGainMeters: 410, Rpe.Of(7))
            {
                Source = new Formed(),
                Facets = [],
            });
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Non_finite_targets_are_refused_when_the_prescription_is_built(double measure)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LiftPrescription(Guid.NewGuid(), TargetReps: 5, TargetWeight: measure, WeightUnit.Pounds, Rpe.Of(8)));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DistanceTimePrescription(Guid.NewGuid(), measure, DistanceUnit.Meters, TimeSpan.FromMinutes(8)));
    }

    /// <summary>
    /// A corrupt row cannot smuggle one in either: the guard sits in the constructor, which is
    /// the only way System.Text.Json can build these records.
    /// </summary>
    [Fact]
    public void A_non_finite_measure_in_stored_json_fails_loudly_on_read()
    {
        Assert.ThrowsAny<Exception>(() => System.Text.Json.JsonSerializer.Deserialize<SetEntry>(
            """{"$type":"trailRun","Distance":1e400,"Unit":2,"Duration":"01:04:00","ElevationGainMeters":410,"Rpe":7,"Source":{"$type":"formed"},"Facets":[]}"""));
    }

    /// <summary>
    /// Sign is deliberately not policed (owner's call, 2026-07-20): net elevation is legitimately
    /// negative on a descent, and no distance or weight bound has been agreed. Characterizes the
    /// gap so it stays a decision rather than an oversight.
    /// </summary>
    [Fact]
    public void Negative_measures_are_accepted_by_design_for_now()
    {
        var set = new TrailRunSet(Distance: -5, DistanceUnit.Miles, TimeSpan.FromMinutes(-10), ElevationGainMeters: -100, Rpe.Of(5))
        {
            Source = new Formed(),
            Facets = [],
        };

        using (var context = _db.CreateContext())
        {
            context.Sessions.Add(Some.CompletedSession(_db.UserId) with { Sets = [set] });
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        var loaded = Assert.IsType<TrailRunSet>(Assert.Single(((CompletedTrainingSession)fresh.Sessions.Single()).Sets));
        Assert.Equal(-5, loaded.Distance);
    }

    [Fact]
    public void A_zero_length_interval_round_trips_as_a_zero_duration()
    {
        var moment = new DateTime(2026, 7, 18, 9, 30, 0, DateTimeKind.Utc);

        using (var context = _db.CreateContext())
        {
            context.Sessions.Add(Some.CompletedSession(_db.UserId) with { Period = new Interval(moment, moment) });
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        var interval = Assert.IsType<Interval>(((CompletedTrainingSession)fresh.Sessions.Single()).Period);
        Assert.Equal(TimeSpan.Zero, interval.Duration);
    }

    [Fact]
    public void A_very_long_note_round_trips_intact()
    {
        var essay = string.Concat(Enumerable.Repeat("every rep felt heavy. ", 20_000));

        using (var context = _db.CreateContext())
        {
            context.Sessions.Add(Some.CompletedSession(_db.UserId) with { Notes = essay });
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        Assert.Equal(essay, fresh.Sessions.Single().Notes);
    }
}
