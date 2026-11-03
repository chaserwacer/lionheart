using lionheart.Functional;
using lionheart.Model.Chat.Memory;
using lionheart.Model.Chat.Thread;
using lionheart.Tests.Support;

namespace lionheart.Tests.ChatThread;

/// <summary>
/// A memory changes by being superseded, never by being rewritten, and a session is distilled once.
/// </summary>
public sealed class MemoryLifecycleTests
{
    private static readonly Guid Athlete = Guid.NewGuid();

    [Fact]
    public void Revising_a_memory_supersedes_it_with_a_current_successor_in_the_same_lineage()
    {
        var knee = AMemory.For(Athlete).OfKind(MemoryKind.Injury).Saying("Left knee pain on deep squats.").Build();
        var at = Some.Clock.AddDays(20);
        var successorId = Guid.NewGuid();

        var successor = ResultAssert.Succeeded(knee.Revise(
            successorId, MemoryKind.Fact, "Left knee pain resolved as of 2026-08-07.", new MemoryOrigin.Corrected(), at));

        Assert.Equal(new MemoryStanding.Superseded(successorId, at), knee.Standing);
        Assert.True(successor.IsCurrent);
        Assert.Equal(knee.LineageId, successor.LineageId);
        Assert.Equal(MemoryKind.Fact, successor.Kind);
        Assert.Equal(at, successor.RecordedAt);
    }

    [Fact]
    public void A_memory_already_superseded_cannot_be_revised_again()
    {
        var knee = AMemory.For(Athlete).Build();
        ResultAssert.Succeeded(knee.Revise(Guid.NewGuid(), MemoryKind.Fact, "Second.", new MemoryOrigin.Corrected(), Some.Clock));

        var problem = ResultAssert.Failed(knee.Revise(Guid.NewGuid(), MemoryKind.Fact, "Third.", new MemoryOrigin.Corrected(), Some.Clock));

        Assert.IsType<Problem.Conflict>(problem);
    }

    [Fact]
    public void Retiring_ends_a_memory_with_nothing_in_its_place()
    {
        var commitment = AMemory.For(Athlete).OfKind(MemoryKind.Commitment).Build();

        ResultAssert.Succeeded(commitment.Retire(Some.Clock, "Done."));

        Assert.Equal(new MemoryStanding.Retired(Some.Clock, "Done."), commitment.Standing);
        Assert.IsType<Problem.Conflict>(ResultAssert.Failed(commitment.Retire(Some.Clock, "Again.")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_memory_must_say_something(string text) =>
        Assert.IsType<Problem.Invalid>(ResultAssert.Failed(
            ChatMemory.Record(Guid.NewGuid(), Athlete, MemoryKind.Fact, text, new MemoryOrigin.Corrected(), Some.Clock)));

    [Fact]
    public void A_memory_longer_than_the_limit_is_refused() =>
        Assert.IsType<Problem.Invalid>(ResultAssert.Failed(ChatMemory.Record(
            Guid.NewGuid(), Athlete, MemoryKind.Fact, new string('a', ChatMemory.MaxTextLength + 1),
            new MemoryOrigin.Corrected(), Some.Clock)));

    [Fact]
    public void A_kind_outside_the_set_is_refused() =>
        Assert.IsType<Problem.Invalid>(ResultAssert.Failed(ChatMemory.Record(
            Guid.NewGuid(), Athlete, (MemoryKind)42, "Something.", new MemoryOrigin.Corrected(), Some.Clock)));

    [Theory]
    [InlineData(MemoryKind.Goal, true)]
    [InlineData(MemoryKind.Injury, true)]
    [InlineData(MemoryKind.Preference, true)]
    [InlineData(MemoryKind.Commitment, false)]
    [InlineData(MemoryKind.Decision, false)]
    [InlineData(MemoryKind.Fact, false)]
    public void Goals_injuries_and_preferences_are_the_standing_core(MemoryKind kind, bool isCore) =>
        Assert.Equal(isCore, kind.IsCore);

    // ---- Sessions ----

    [Fact]
    public void A_session_is_consolidated_once()
    {
        var session = ChatThreadSession.Open(Guid.NewGuid(), Athlete, Some.Clock);
        ResultAssert.Succeeded(session.Consolidated(Some.Clock, 1, 0, 0));

        Assert.IsType<Problem.Conflict>(ResultAssert.Failed(session.Consolidated(Some.Clock, 1, 0, 0)));
        Assert.False(session.IsDueForConsolidation(Some.Clock.AddDays(1)));
    }

    [Fact]
    public void A_failed_session_is_due_again_after_its_back_off_and_until_its_attempts_run_out()
    {
        var session = ChatThreadSession.Open(Guid.NewGuid(), Athlete, Some.Clock);
        var failedAt = Some.Clock.AddHours(4);

        session.ConsolidationFailed(failedAt, "model unavailable");

        Assert.False(session.IsDueForConsolidation(failedAt.AddMinutes(10)));
        Assert.True(session.IsDueForConsolidation(failedAt + ChatSessionPolicy.ConsolidationRetryAfter));

        for (var attempt = 2; attempt <= ChatSessionPolicy.ConsolidationAttempts; attempt++)
        {
            session.ConsolidationFailed(failedAt, "model unavailable");
        }

        Assert.Equal(ChatSessionPolicy.ConsolidationAttempts,
            Assert.IsType<SessionConsolidation.Failed>(session.Consolidation).Attempts);
        Assert.False(session.IsDueForConsolidation(failedAt.AddDays(1)));
    }

    [Fact]
    public void A_late_failure_does_not_undo_a_consolidation()
    {
        var session = ChatThreadSession.Open(Guid.NewGuid(), Athlete, Some.Clock);
        ResultAssert.Succeeded(session.Consolidated(Some.Clock, 0, 0, 0));

        session.ConsolidationFailed(Some.Clock, "late");

        Assert.True(session.IsConsolidated);
    }
}
