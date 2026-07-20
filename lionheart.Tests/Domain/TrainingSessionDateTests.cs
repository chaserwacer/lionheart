using lionheart.Model.Analysis;
using lionheart.Tests.Support;

namespace lionheart.Tests.Domain;

/// <summary>
/// Date is derived from each lifecycle state's own time-truth — these pin the derivation
/// the doc promises can never drift from the state it came from.
/// </summary>
public class TrainingSessionDateTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Planned_session_dates_from_its_planned_day()
    {
        var session = Some.PlannedSession(UserId);

        Assert.Equal(session.PlannedFor, session.Date);
    }

    [Fact]
    public void Skipped_session_dates_from_its_planned_day()
    {
        var session = Some.SkippedSession(UserId);

        Assert.Equal(session.PlannedFor, session.Date);
    }

    [Fact]
    public void Active_session_dates_from_its_start_moment()
    {
        var session = Some.ActiveSession(UserId);

        Assert.Equal(DateOnly.FromDateTime(session.StartedAt), session.Date);
    }

    [Fact]
    public void Completed_session_dates_from_its_performed_interval_start()
    {
        var session = Some.CompletedSession(UserId);

        Assert.Equal(DateOnly.FromDateTime(session.Period.StartTime), session.Date);
    }

    [Fact]
    public void Completed_session_dates_from_its_performed_instant()
    {
        var at = new DateTime(2026, 7, 4, 18, 0, 0, DateTimeKind.Utc);
        var session = Some.CompletedSession(UserId) with { Period = new Instant(at) };

        Assert.Equal(new DateOnly(2026, 7, 4), session.Date);
    }
}
