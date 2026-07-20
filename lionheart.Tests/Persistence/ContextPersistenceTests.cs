using lionheart.Model.Contexts;
using lionheart.Tests.Support;

namespace lionheart.Tests.Persistence;

public sealed class ContextPersistenceTests : IDisposable
{
    private readonly RemodelDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private Context SaveAndRehydrate(Context context)
    {
        using (var ctx = _db.CreateContext())
        {
            ctx.Contexts.Add(context);
            ctx.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        return fresh.Contexts.Single(c => c.ContextId == context.ContextId);
    }

    [Fact]
    public void Exertion_context_round_trips_with_effort_period_and_decoration()
    {
        var context = Some.ExertionContext(_db.UserId);

        var loaded = Assert.IsType<ExertionContext>(SaveAndRehydrate(context));

        Assert.Equal(context.ContextId, loaded.ContextId);
        Assert.Equal(context.UserId, loaded.UserId);
        Assert.Equal(context.Notes, loaded.Notes);
        Assert.Equal(context.Period, loaded.Period);
        JsonAssert.Equal(context.Effort, loaded.Effort);
        JsonAssert.Equal(context.Decoration, loaded.Decoration);
    }

    [Fact]
    public void State_context_round_trips_undecorated_at_an_instant()
    {
        var context = Some.StateContext(_db.UserId);

        var loaded = Assert.IsType<StateContext>(SaveAndRehydrate(context));

        Assert.Equal(context.ContextId, loaded.ContextId);
        Assert.Equal(context.Period, loaded.Period);
        Assert.False(loaded.Decoration.IsDecorated);
    }

    [Fact]
    public void Both_context_kinds_coexist_and_rehydrate_as_their_own_types()
    {
        using (var ctx = _db.CreateContext())
        {
            ctx.Contexts.AddRange(Some.ExertionContext(_db.UserId), Some.StateContext(_db.UserId));
            ctx.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        var loaded = fresh.Contexts.ToList();

        Assert.Equal(2, loaded.Count);
        Assert.Single(loaded.OfType<ExertionContext>());
        Assert.Single(loaded.OfType<StateContext>());
    }
}
