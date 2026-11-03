using lionheart.Model.Training.Sessions;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Persistence;

/// <summary>
/// What the relational spine does at its edges: deleting the owning user, pointing at a user
/// that is not there, and the deliberately unconstrained program pointer. The membership
/// sentinel means Sessions.TrainingProgramId carries no foreign key, so nothing but these
/// tests says what a dangling program id does.
/// </summary>
public sealed class RelationalEdgeTests : IDisposable
{
    private readonly RemodelDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private void DeleteTheUser()
    {
        using var context = _db.CreateContext();
        context.LionheartUsers.Remove(context.LionheartUsers.Single(u => u.UserID == _db.UserId));
        context.SaveChanges();
    }

    [Fact]
    public void Deleting_a_user_cascades_to_their_sessions()
    {
        using (var context = _db.CreateContext())
        {
            context.Sessions.Add(Some.CompletedSession(_db.UserId));
            context.SaveChanges();
        }

        DeleteTheUser();

        using var fresh = _db.CreateContext();
        Assert.Empty(fresh.Sessions);
    }

    [Fact]
    public void Deleting_a_user_cascades_to_their_contexts()
    {
        using (var context = _db.CreateContext())
        {
            context.Contexts.Add(Some.ExertionContext(_db.UserId));
            context.SaveChanges();
        }

        DeleteTheUser();

        using var fresh = _db.CreateContext();
        Assert.Empty(fresh.Contexts);
    }

    [Fact]
    public void Deleting_a_user_cascades_to_their_programs()
    {
        using (var context = _db.CreateContext())
        {
            context.Programs.Add(Some.ActiveProgram(_db.UserId));
            context.SaveChanges();
        }

        DeleteTheUser();

        using var fresh = _db.CreateContext();
        Assert.Empty(fresh.Programs);
    }

    /// <summary>
    /// AnalyzableEntry is a projection of its owner's sessions and contexts, so it follows them
    /// into the grave. Without the mapped relationship no foreign key is emitted and the cascade
    /// misses this table entirely, leaving every summary text and embedding the user ever
    /// produced live in the one table the retrieve primitive reads.
    /// </summary>
    [Fact]
    public void Deleting_a_user_cascades_to_their_analyzable_entries()
    {
        using (var context = _db.CreateContext())
        {
            context.AnalyzableEntries.Add(Some.AnalyzableEntry(_db.UserId));
            context.SaveChanges();
        }

        DeleteTheUser();

        using var fresh = _db.CreateContext();
        Assert.Empty(fresh.AnalyzableEntries);
    }

    [Fact]
    public void An_analyzable_entry_for_a_nonexistent_user_is_rejected()
    {
        using var context = _db.CreateContext();
        context.AnalyzableEntries.Add(Some.AnalyzableEntry(Guid.NewGuid()));

        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
    }

    [Fact]
    public void A_session_for_a_nonexistent_user_is_rejected()
    {
        using var context = _db.CreateContext();
        context.Sessions.Add(Some.CompletedSession(Guid.NewGuid()));

        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
    }

    [Fact]
    public void A_context_for_a_nonexistent_user_is_rejected()
    {
        using var context = _db.CreateContext();
        context.Contexts.Add(Some.StateContext(Guid.NewGuid()));

        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
    }

    /// <summary>
    /// The membership sentinel buys schema-flexibility at the cost of referential integrity:
    /// a session may name a program that no longer exists. The contract is that it rehydrates
    /// as a normal InProgram rather than collapsing into Standalone or throwing.
    /// </summary>
    [Fact]
    public void A_session_in_a_deleted_program_keeps_its_membership()
    {
        var program = Some.ActiveProgram(_db.UserId);
        var session = Some.CompletedSession(_db.UserId) with { Membership = new InProgram(program.TrainingProgramId) };

        using (var context = _db.CreateContext())
        {
            context.Programs.Add(program);
            context.Sessions.Add(session);
            context.SaveChanges();
        }

        using (var context = _db.CreateContext())
        {
            context.Programs.Remove(context.Programs.Single());
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        var membership = Assert.IsType<InProgram>(fresh.Sessions.Single().Membership);
        Assert.Equal(program.TrainingProgramId, membership.TrainingProgramId);
        Assert.Empty(fresh.Programs);
    }

    [Fact]
    public void A_session_may_name_a_program_that_never_existed()
    {
        var strangerId = Guid.NewGuid();
        var session = Some.CompletedSession(_db.UserId) with { Membership = new InProgram(strangerId) };

        using (var context = _db.CreateContext())
        {
            context.Sessions.Add(session);
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        Assert.Equal(strangerId, Assert.IsType<InProgram>(fresh.Sessions.Single().Membership).TrainingProgramId);
    }

    /// <summary>
    /// The sentinel's other half: a program whose id happened to be Guid.Empty would rehydrate
    /// every one of its sessions as Standalone. InProgram refuses to be built that way, so the
    /// only route in is hand-written SQL — and it stays indistinguishable from Standalone.
    /// </summary>
    [Fact]
    public void The_empty_guid_reads_back_as_standalone_whatever_wrote_it()
    {
        _db.ExecuteNonQuery($$"""
            INSERT INTO Sessions
                (TrainingSessionId, UserId, Notes, Provenance, TrainingProgramId, RoutineId, CreatedAt, Lifecycle, PlannedFor)
            VALUES ('{{RemodelDatabase.SqlGuid(Guid.NewGuid())}}', '{{RemodelDatabase.SqlGuid(_db.UserId)}}',
                    'planted', 0, '{{RemodelDatabase.SqlGuid(Guid.Empty)}}', '{{RemodelDatabase.SqlGuid(Guid.Empty)}}', '2026-07-18 09:30:00',
                    'skipped', '2026-07-21');
            """);

        using var fresh = _db.CreateContext();
        Assert.IsType<Standalone>(fresh.Sessions.Single().Membership);
    }
}
