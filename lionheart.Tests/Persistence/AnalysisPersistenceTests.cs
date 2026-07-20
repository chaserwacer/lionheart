using lionheart.Model.Analysis;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Persistence;

public sealed class AnalysisPersistenceTests : IDisposable
{
    private readonly RemodelDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Analyzable_entry_round_trips_including_its_embedding_blob()
    {
        var entry = Some.AnalyzableEntry(_db.UserId);

        using (var context = _db.CreateContext())
        {
            context.AnalyzableEntries.Add(entry);
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        var loaded = fresh.AnalyzableEntries.Single(e => e.EntryId == entry.EntryId);

        Assert.Equal(entry.Kind, loaded.Kind);
        Assert.Equal(entry.UserId, loaded.UserId);
        Assert.Equal(entry.Period, loaded.Period);
        JsonAssert.Equal(entry.Facets, loaded.Facets);
        Assert.Equal(entry.Valence, loaded.Valence);
        Assert.Equal(entry.Embedding, loaded.Embedding);
        Assert.Equal(entry.SummaryText, loaded.SummaryText);
        Assert.Equal(entry.DecoratorVersion, loaded.DecoratorVersion);
    }

    [Fact]
    public void Empty_embedding_round_trips_as_empty()
    {
        var entry = Some.AnalyzableEntry(_db.UserId) with { Embedding = [] };

        using (var context = _db.CreateContext())
        {
            context.AnalyzableEntries.Add(entry);
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        Assert.Empty(fresh.AnalyzableEntries.Single().Embedding);
    }

    [Fact]
    public void Facet_value_round_trips_under_its_composite_key()
    {
        using (var context = _db.CreateContext())
        {
            context.FacetValues.Add(Some.FacetValue);
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        var loaded = fresh.FacetValues.Single(v => v.Axis == FacetAxis.Modality && v.Canonical == "lift");

        Assert.Equal(Some.FacetValue.Aliases, loaded.Aliases);
        Assert.Equal(Some.FacetValue.Embedding, loaded.Embedding);
    }

    [Fact]
    public void Same_canonical_spelling_may_exist_on_different_axes()
    {
        using (var context = _db.CreateContext())
        {
            context.FacetValues.AddRange(
                new FacetValue(FacetAxis.Intent, "recovery", [], Some.Embedding),
                new FacetValue(FacetAxis.BodyState, "recovery", [], Some.Embedding));
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        Assert.Equal(2, fresh.FacetValues.Count(v => v.Canonical == "recovery"));
    }

    [Fact]
    public void Duplicate_canonical_on_one_axis_is_rejected_by_the_key()
    {
        using (var context = _db.CreateContext())
        {
            context.FacetValues.Add(Some.FacetValue);
            context.SaveChanges();
        }

        using var second = _db.CreateContext();
        second.FacetValues.Add(Some.FacetValue with { Aliases = ["other-alias"] });
        Assert.Throws<DbUpdateException>(() => second.SaveChanges());
    }
}
