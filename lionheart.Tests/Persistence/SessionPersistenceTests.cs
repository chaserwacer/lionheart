using lionheart.Model.Training.Sessions;
using lionheart.Tests.Support;

namespace lionheart.Tests.Persistence;

/// <summary>
/// The docs/data-remodel.md step-3 smoke deliverable: every session lifecycle state saved
/// through EF and rehydrated from SQLite by a fresh context, JSON columns and all.
/// </summary>
public sealed class SessionPersistenceTests : IDisposable
{
    private readonly RemodelDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private TrainingSession SaveAndRehydrate(TrainingSession session)
    {
        using (var context = _db.CreateContext())
        {
            context.Sessions.Add(session);
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        return fresh.Sessions.Single(s => s.TrainingSessionId == session.TrainingSessionId);
    }

    private void AssertBaseSurvived(TrainingSession expected, TrainingSession actual)
    {
        Assert.Equal(expected.TrainingSessionId, actual.TrainingSessionId);
        Assert.Equal(expected.UserId, actual.UserId);
        Assert.Equal(expected.Notes, actual.Notes);
        Assert.Equal(expected.Provenance, actual.Provenance);
        Assert.Equal(expected.Membership, actual.Membership);
        Assert.Equal(expected.Origin, actual.Origin);
        Assert.Equal(expected.CreatedAt, actual.CreatedAt);
        Assert.Equal(expected.Date, actual.Date);
    }

    [Fact]
    public void Planned_session_round_trips_with_every_prescription_shape()
    {
        var session = Some.PlannedSession(_db.UserId);

        var loaded = Assert.IsType<PlannedTrainingSession>(SaveAndRehydrate(session));

        AssertBaseSurvived(session, loaded);
        Assert.Equal(session.PlannedFor, loaded.PlannedFor);
        JsonAssert.Equal(session.Prescriptions, loaded.Prescriptions);
    }

    [Fact]
    public void Active_session_round_trips_with_every_set_shape()
    {
        var session = Some.ActiveSession(_db.UserId);

        var loaded = Assert.IsType<ActiveTrainingSession>(SaveAndRehydrate(session));

        AssertBaseSurvived(session, loaded);
        Assert.Equal(session.StartedAt, loaded.StartedAt);
        JsonAssert.Equal(session.Sets, loaded.Sets);
    }

    [Fact]
    public void Completed_session_round_trips_period_effort_and_decoration()
    {
        var session = Some.CompletedSession(_db.UserId);

        var loaded = Assert.IsType<CompletedTrainingSession>(SaveAndRehydrate(session));

        AssertBaseSurvived(session, loaded);
        JsonAssert.Equal(session.Sets, loaded.Sets);
        Assert.Equal(session.Period, loaded.Period);
        JsonAssert.Equal(session.Effort, loaded.Effort);
        JsonAssert.Equal(session.Decoration, loaded.Decoration);
    }

    [Fact]
    public void Skipped_session_round_trips()
    {
        var session = Some.SkippedSession(_db.UserId);

        var loaded = Assert.IsType<SkippedTrainingSession>(SaveAndRehydrate(session));

        AssertBaseSurvived(session, loaded);
        Assert.Equal(session.PlannedFor, loaded.PlannedFor);
    }

    [Fact]
    public void All_lifecycle_states_coexist_and_rehydrate_as_their_own_types()
    {
        using (var context = _db.CreateContext())
        {
            context.Sessions.AddRange(
                Some.PlannedSession(_db.UserId),
                Some.ActiveSession(_db.UserId),
                Some.CompletedSession(_db.UserId),
                Some.SkippedSession(_db.UserId));
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        var loaded = fresh.Sessions.ToList();

        Assert.Equal(4, loaded.Count);
        Assert.Single(loaded.OfType<PlannedTrainingSession>());
        Assert.Single(loaded.OfType<ActiveTrainingSession>());
        Assert.Single(loaded.OfType<CompletedTrainingSession>());
        Assert.Single(loaded.OfType<SkippedTrainingSession>());
    }

    [Fact]
    public void In_program_membership_round_trips_its_program_id()
    {
        var program = Some.ActiveProgram(_db.UserId);
        var session = Some.CompletedSession(_db.UserId) with { Membership = new InProgram(program.TrainingProgramId) };

        using (var context = _db.CreateContext())
        {
            context.Programs.Add(program);
            context.Sessions.Add(session);
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        var membership = Assert.IsType<InProgram>(fresh.Sessions.Single().Membership);
        Assert.Equal(program.TrainingProgramId, membership.TrainingProgramId);
    }

    [Fact]
    public void Standalone_membership_persists_as_the_empty_guid_sentinel()
    {
        var session = Some.CompletedSession(_db.UserId);
        Assert.IsType<Standalone>(session.Membership);

        using (var context = _db.CreateContext())
        {
            context.Sessions.Add(session);
            context.SaveChanges();
        }

        var stored = Guid.Parse((string)_db.ExecuteScalar("SELECT TrainingProgramId FROM Sessions")!);
        Assert.Equal(Guid.Empty, stored);

        using var fresh = _db.CreateContext();
        Assert.IsType<Standalone>(fresh.Sessions.Single().Membership);
    }
}
