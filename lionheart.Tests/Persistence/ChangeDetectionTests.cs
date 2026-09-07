using lionheart.Model.Analysis;
using lionheart.Model.Training.Sessions;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Persistence;

/// <summary>
/// Every other persistence test replaces the whole entity, which hides the question that
/// actually decides whether edits reach the database: does EF notice a mutation behind a
/// value converter? A converted property is compared by its ValueComparer, not by reference,
/// so a wrong comparer means silently dropped writes (or a write on every save).
/// </summary>
public sealed class ChangeDetectionTests : IDisposable
{
    private readonly RemodelDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private CompletedTrainingSession Insert()
    {
        var session = Some.CompletedSession(_db.UserId);
        using var context = _db.CreateContext();
        context.Sessions.Add(session);
        context.SaveChanges();
        return session;
    }

    [Fact]
    public void Replacing_a_json_list_with_different_content_is_detected()
    {
        var session = Insert();

        using (var context = _db.CreateContext())
        {
            var tracked = (CompletedTrainingSession)context.Sessions.Single();
            tracked.Sets.ToList(); // materialize before swapping, mirroring a real edit path
            context.Entry(tracked).Property(s => s.Sets).CurrentValue = [Some.LiftSet];

            Assert.Equal(EntityState.Modified, context.Entry(tracked).State);
            Assert.Equal(1, context.SaveChanges());
        }

        using var fresh = _db.CreateContext();
        Assert.Single(((CompletedTrainingSession)fresh.Sessions.Single()).Sets);
    }

    /// <summary>
    /// A fresh list instance holding equal content must NOT be written: the comparer compares
    /// serialized form, not reference identity. If this regresses, every read-then-save issues
    /// a pointless UPDATE of the largest columns in the schema.
    /// </summary>
    [Fact]
    public void Replacing_a_json_list_with_an_equal_instance_is_not_written()
    {
        var session = Insert();

        using var context = _db.CreateContext();
        var tracked = (CompletedTrainingSession)context.Sessions.Single();
        context.Entry(tracked).Property(s => s.Sets).CurrentValue = tracked.Sets.ToList();

        Assert.Equal(0, context.SaveChanges());
    }

    [Fact]
    public void Reloading_an_untouched_entity_writes_nothing()
    {
        Insert();

        using var context = _db.CreateContext();
        _ = context.Sessions.Single();

        Assert.Equal(0, context.SaveChanges());
        Assert.DoesNotContain(context.ChangeTracker.Entries(), e => e.State == EntityState.Modified);
    }

    [Fact]
    public void Reloading_an_untouched_context_and_analyzable_entry_writes_nothing()
    {
        using (var seed = _db.CreateContext())
        {
            seed.Contexts.Add(Some.ExertionContext(_db.UserId));
            seed.AnalyzableEntries.Add(Some.AnalyzableEntry(_db.UserId));
            seed.SaveChanges();
        }

        using var context = _db.CreateContext();
        _ = context.Contexts.Single();
        _ = context.AnalyzableEntries.Single();

        Assert.Equal(0, context.SaveChanges());
    }

    /// <summary>
    /// The vector comparer hashes on Length alone. A cheap hash is only safe if equality is
    /// exact — otherwise two same-length vectors would be treated as unchanged and the new
    /// embedding would never be written. This is the case that would prove it wrong.
    /// </summary>
    [Fact]
    public void Same_length_different_content_embedding_is_still_detected()
    {
        using (var seed = _db.CreateContext())
        {
            seed.AnalyzableEntries.Add(Some.AnalyzableEntry(_db.UserId));
            seed.SaveChanges();
        }

        float[] replacement = [9.5f, 9.5f, 9.5f, 9.5f];
        using (var context = _db.CreateContext())
        {
            var tracked = context.AnalyzableEntries.Single();
            Assert.Equal(tracked.Embedding.Length, replacement.Length);
            context.Entry(tracked).Property(e => e.Embedding).CurrentValue = replacement;

            Assert.Equal(1, context.SaveChanges());
        }

        using var fresh = _db.CreateContext();
        Assert.Equal(replacement, fresh.AnalyzableEntries.Single().Embedding);
    }

    [Fact]
    public void Identical_embedding_content_in_a_new_array_is_not_written()
    {
        using (var seed = _db.CreateContext())
        {
            seed.AnalyzableEntries.Add(Some.AnalyzableEntry(_db.UserId));
            seed.SaveChanges();
        }

        using var context = _db.CreateContext();
        var tracked = context.AnalyzableEntries.Single();
        context.Entry(tracked).Property(e => e.Embedding).CurrentValue = tracked.Embedding.ToArray();

        Assert.Equal(0, context.SaveChanges());
    }

    /// <summary>
    /// Membership converts to a Guid with no explicit comparer, so it leans on EF's default for
    /// the model type. Record equality has to carry the change through, or a session moved into
    /// or out of a program is silently not saved.
    /// </summary>
    [Fact]
    public void Moving_a_session_into_a_program_is_detected()
    {
        Insert();
        var programId = Guid.NewGuid();

        using (var context = _db.CreateContext())
        {
            var tracked = context.Sessions.Single();
            context.Entry(tracked).Property(s => s.Membership).CurrentValue = new InProgram(programId);

            Assert.Equal(1, context.SaveChanges());
        }

        using var fresh = _db.CreateContext();
        Assert.Equal(programId, Assert.IsType<InProgram>(fresh.Sessions.Single().Membership).TrainingProgramId);
    }

    [Fact]
    public void Moving_a_session_out_of_a_program_is_detected()
    {
        var session = Some.CompletedSession(_db.UserId) with { Membership = new InProgram(Guid.NewGuid()) };
        using (var seed = _db.CreateContext())
        {
            seed.Sessions.Add(session);
            seed.SaveChanges();
        }

        using (var context = _db.CreateContext())
        {
            var tracked = context.Sessions.Single();
            context.Entry(tracked).Property(s => s.Membership).CurrentValue = new Standalone();

            Assert.Equal(1, context.SaveChanges());
        }

        using var fresh = _db.CreateContext();
        Assert.IsType<Standalone>(fresh.Sessions.Single().Membership);
    }

    [Fact]
    public void Re_setting_the_same_membership_state_is_not_written()
    {
        Insert();

        using var context = _db.CreateContext();
        var tracked = context.Sessions.Single();
        context.Entry(tracked).Property(s => s.Membership).CurrentValue = new Standalone();

        Assert.Equal(0, context.SaveChanges());
    }

    [Fact]
    public void Replacing_a_decoration_state_is_detected()
    {
        using (var seed = _db.CreateContext())
        {
            seed.Contexts.Add(Some.StateContext(_db.UserId));
            seed.SaveChanges();
        }

        using (var context = _db.CreateContext())
        {
            var tracked = context.Contexts.Single();
            Assert.False(tracked.Decoration.IsDecorated);
            context.Entry(tracked).Property(c => c.Decoration).CurrentValue = new Decorated(Some.Decoration);

            Assert.Equal(1, context.SaveChanges());
        }

        using var fresh = _db.CreateContext();
        Assert.True(fresh.Contexts.Single().Decoration.IsDecorated);
    }
}
