using System.Diagnostics;
using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Chat;
using lionheart.Model.Chat.Memory;
using lionheart.Model.Chat.Thread;
using lionheart.Services.Chat.Memory;
using lionheart.Services.Llm;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Chat.Thread;

/// <summary>What a pass over ended sessions did.</summary>
/// <param name="Failures">One line per session that could not be consolidated, saying why.</param>
public sealed record ConsolidationRun(int Consolidated, IReadOnlyList<string> Failures)
{
    public static ConsolidationRun Nothing { get; } = new(0, []);
}

/// <summary>Distils ended thread sessions into durable memory.</summary>
public interface ISessionConsolidator
{
    /// <summary>
    /// Every session of this athlete still owed a consolidation, oldest first. Called when a new
    /// session is about to open, so every earlier session counts as ended.
    /// </summary>
    Task<ConsolidationRun> ConsolidateEndedAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Every athlete's ended sessions still owed one: idle past the gap, or followed by a newer session.
    /// </summary>
    Task<ConsolidationRun> ConsolidateAllEndedAsync(CancellationToken ct);
}

/// <summary>
/// One model call per session, asking for schema-valid memory changes over the session's turns and
/// the memories it may change; then the changes are applied and the session is marked, in one save.
/// </summary>
/// <remarks>
/// Uses the flagship chat model at the shallowest reasoning it allows — on Anthropic,
/// <c>claude-opus-5-5</c> at low effort. Deciding that "my knee is fine now" revises the knee
/// memory rather than adding a second one is judgement over the whole session, which is worth the
/// better model; it is still extraction into a fixed schema, which is not worth deep reasoning, and
/// on LM Studio the minimal depth is what keeps a schema answer out of the reasoning channel.
/// <para>
/// A failure is recorded on the session rather than thrown, and the session is retried later. A
/// change the model got wrong — a handle that names nothing, two changes to one memory — is set
/// aside and the rest applied.
/// </para>
/// </remarks>
public sealed class SessionConsolidator(
    ModelContext context,
    IChatModel model,
    MemoryVectors vectors,
    TimeProvider clock,
    ILogger<SessionConsolidator> logger) : ISessionConsolidator
{
    /// <summary>
    /// Room for the changes and for the thinking that precedes them, which a provider whose thinking
    /// cannot be switched off counts against the same cap.
    /// </summary>
    private const int ConsolidationTokenBudget = 16_000;

    private readonly ModelContext _context = context;
    private readonly IChatModel _model = model;
    private readonly MemoryVectors _vectors = vectors;
    private readonly TimeProvider _clock = clock;
    private readonly ILogger<SessionConsolidator> _logger = logger;

    public async Task<ConsolidationRun> ConsolidateEndedAsync(Guid userId, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var owed = await _context.ChatThreadSessions
            .Where(session => session.UserId == userId
                && !EF.Property<bool>(session, ChatThreadConfiguration.SessionIsConsolidatedColumn))
            .OrderBy(session => session.OpenedAt)
            .ToListAsync(ct);

        return await RunAsync(owed.Where(session => session.IsDueForConsolidation(now)), ct);
    }

    public async Task<ConsolidationRun> ConsolidateAllEndedAsync(CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var idleSince = now - ChatSessionPolicy.IdleGap;
        var owed = await _context.ChatThreadSessions
            .Where(session => !EF.Property<bool>(session, ChatThreadConfiguration.SessionIsConsolidatedColumn))
            .Select(session => new
            {
                Session = session,
                LastActivityAt = _context.ChatConversations
                    .Where(conversation => conversation.ChatConversationID == session.ChatConversationId)
                    .Select(conversation => conversation.LastUpdate)
                    .First(),
                IsNewest = !_context.ChatThreadSessions.Any(later =>
                    later.UserId == session.UserId && later.OpenedAt > session.OpenedAt),
            })
            .ToListAsync(ct);

        return await RunAsync(
            owed
                .Where(candidate => !candidate.IsNewest || candidate.LastActivityAt <= idleSince)
                .Select(candidate => candidate.Session)
                .Where(session => session.IsDueForConsolidation(now))
                .OrderBy(session => session.OpenedAt),
            ct);
    }

    /// <summary>
    /// One session after another, oldest first, so a later session can revise what an earlier one
    /// remembered. A failure does not stop the pass: it is recorded on its session and the rest go on.
    /// </summary>
    private async Task<ConsolidationRun> RunAsync(IEnumerable<ChatThreadSession> sessions, CancellationToken ct)
    {
        var run = ConsolidationRun.Nothing;
        foreach (var session in sessions)
        {
            var outcome = await ConsolidateAsync(session, ct);
            run = outcome.Match(
                _ => run with { Consolidated = run.Consolidated + 1 },
                problem => run with { Failures = [.. run.Failures, $"{session.ChatConversationId}: {problem.Description}"] });
        }

        return run;
    }

    /// <summary>
    /// Read, ask, stage, mark, save. A failure anywhere is saved instead, as the record that this
    /// attempt failed — either way, one save.
    /// </summary>
    private async Task<Result<ChatThreadSession, Problem>> ConsolidateAsync(
        ChatThreadSession session, CancellationToken ct)
    {
        var now = _clock.GetUtcNow();
        var transcript = await TranscriptAsync(session, ct);
        var current = await CurrentAsync(session.UserId, ct);
        var prompt = ConsolidationPrompt.For(
            session.OpenedAt, transcript, await CandidatesAsync(current, transcript), _clock.LocalTimeZone);

        return await PlanAsync(prompt, transcript)
            .TapAsync(plan => Report(session, plan))
            .MapAsync(plan => Stage(session, plan, current, now))
            .BindAsync(staged => session
                .Consolidated(now.UtcDateTime, staged.Remembered, staged.Revised, staged.Retired)
                .Map(_ => staged))
            .MapAsync(staged => KeepAsync(staged, current, ct))
            .TapErrorAsync(problem => RecordFailureAsync(session, problem, now, ct))
            .MapAsync(_ => session);
    }

    // ---- Reading ----

    /// <summary>The athlete's and the model's turns, oldest first. Tool results are the app's data, not his words.</summary>
    private async Task<IReadOnlyList<LHChatMessage>> TranscriptAsync(ChatThreadSession session, CancellationToken ct)
    {
        var athlete = await _context.UserChatMessages.AsNoTracking()
            .Where(message => message.ChatConversationID == session.ChatConversationId)
            .ToListAsync(ct);
        var model = await _context.ModelChatMessages.AsNoTracking()
            .Where(message => message.ChatConversationID == session.ChatConversationId)
            .ToListAsync(ct);

        return [.. athlete.Cast<LHChatMessage>().Concat(model).OrderBy(message => message.CreationTime)];
    }

    /// <summary>Tracked, because a consolidation supersedes and retires them.</summary>
    private Task<List<ChatMemory>> CurrentAsync(Guid userId, CancellationToken ct) =>
        _context.ChatMemories
            .Where(memory => memory.UserId == userId
                && EF.Property<bool>(memory, ChatThreadConfiguration.MemoryIsCurrentColumn))
            .ToListAsync(ct);

    private async Task<IReadOnlyList<ChatMemory>> CandidatesAsync(
        IReadOnlyList<ChatMemory> current, IReadOnlyList<LHChatMessage> transcript)
    {
        var said = string.Join("\n", transcript.OfType<LHUserChatMessage>().Select(message => message.Content));
        var vector = current.Count > MemoryRecall.ConsolidationLimit ? await _vectors.OfAsync(said) : [];
        return MemoryRecall.ForConsolidation(current, said, vector);
    }

    // ---- Asking ----

    /// <summary>A session with nothing said in it is consolidated with no changes, without a model call.</summary>
    private Task<Result<ConsolidationPlan, Problem>> PlanAsync(ConsolidationPrompt prompt, IReadOnlyList<LHChatMessage> transcript) =>
        transcript.Count == 0
            ? Task.FromResult(Result<ConsolidationPlan, Problem>.Success(new ConsolidationPlan([], [])))
            : _model
                .RespondAsync(ChatTurnRequest.Extraction(
                    prompt.SystemPrompt, prompt.UserText, ConsolidationPrompt.SchemaName, ConsolidationPrompt.Schema,
                    ConsolidationTokenBudget))
                .BindAsync(turn => ConsolidationParser.Read(turn, prompt));

    private void Report(ChatThreadSession session, ConsolidationPlan plan)
    {
        foreach (var unusable in plan.Unusable)
        {
            _logger.LogWarning(
                "Consolidating session {SessionId} set aside a change: {Reason}", session.ChatConversationId, unusable);
        }
    }

    // ---- Staging ----

    /// <summary>
    /// The plan applied to the current memories, one change at a time. A change the domain refuses
    /// — a memory already changed by an earlier change in this plan — is skipped.
    /// </summary>
    private Staged Stage(ChatThreadSession session, ConsolidationPlan plan, IReadOnlyList<ChatMemory> current, DateTimeOffset now) =>
        plan.Changes.Aggregate(
            Staged.Over(session.UserId, current),
            (staged, change) => staged.Apply(change, session.ChatConversationId, now));

    /// <summary>Embeds what is new, and anything current still waiting for a vector, then saves the consolidation.</summary>
    private async Task<Staged> KeepAsync(Staged staged, IReadOnlyList<ChatMemory> current, CancellationToken ct)
    {
        await _vectors.EmbedAsync(current.Where(memory => memory.IsCurrent).Concat(staged.Added));
        _context.ChatMemories.AddRange(staged.Added);
        await _context.SaveChangesAsync(ct);
        return staged;
    }

    /// <summary>
    /// Undoes what staging did to the current memories and saves the failure on the session, and
    /// nothing else.
    /// </summary>
    private async Task RecordFailureAsync(ChatThreadSession session, Problem problem, DateTimeOffset now, CancellationToken ct)
    {
        foreach (var entry in _context.ChangeTracker.Entries<ChatMemory>().Where(entry => entry.State is EntityState.Modified))
        {
            entry.CurrentValues.SetValues(entry.OriginalValues);
            entry.State = EntityState.Unchanged;
        }

        session.ConsolidationFailed(now.UtcDateTime, problem.Description);
        await _context.SaveChangesAsync(ct);
        _logger.LogWarning(
            "Consolidating session {SessionId} failed: {Reason}", session.ChatConversationId, problem.Description);
    }

    /// <summary>The memory a plan is building: what it added, which memories it has already changed, and the tally.</summary>
    private sealed record Staged(
        Guid UserId,
        IReadOnlyDictionary<Guid, ChatMemory> Current,
        IReadOnlyList<ChatMemory> Added,
        IReadOnlySet<Guid> Changed,
        int Remembered,
        int Revised,
        int Retired)
    {
        public static Staged Over(Guid userId, IReadOnlyList<ChatMemory> current) =>
            new(userId, current.ToDictionary(memory => memory.MemoryId), [], new HashSet<Guid>(), 0, 0, 0);

        public Staged Apply(MemoryChange change, Guid sessionId, DateTimeOffset now) =>
            change switch
            {
                MemoryChange.Remember remember => Repeats(remember.Text)
                    ? this
                    : ChatMemory
                        .Record(Guid.CreateVersion7(now), UserId, remember.Kind, remember.Text,
                            Origin(sessionId, change), now.UtcDateTime)
                        .Match(memory => this with { Added = [.. Added, memory], Remembered = Remembered + 1 }, _ => this),

                MemoryChange.Revise revise => Target(revise.MemoryId)
                    .Bind(memory => memory.Revise(
                        Guid.CreateVersion7(now), revise.Kind, revise.Text, Origin(sessionId, change), now.UtcDateTime))
                    .Match(
                        successor => this with
                        {
                            Added = [.. Added, successor],
                            Changed = new HashSet<Guid>(Changed) { revise.MemoryId },
                            Revised = Revised + 1,
                        },
                        _ => this),

                MemoryChange.Retire retire => Target(retire.MemoryId)
                    .Bind(memory => memory.Retire(now.UtcDateTime, retire.Reason))
                    .Match(
                        _ => this with { Changed = new HashSet<Guid>(Changed) { retire.MemoryId }, Retired = Retired + 1 },
                        _ => this),

                _ => throw new UnreachableException(),
            };

        /// <summary>Whether a memory already kept, or added by this plan, says the same thing.</summary>
        private bool Repeats(string text) =>
            Current.Values.Where(memory => memory.IsCurrent).Concat(Added)
                .Any(memory => string.Equals(memory.Text, text.Trim(), StringComparison.OrdinalIgnoreCase));

        private Result<ChatMemory, Problem> Target(Guid memoryId) =>
            Changed.Contains(memoryId)
                ? Result<ChatMemory, Problem>.Failure(new Problem.Conflict("Already changed by this consolidation."))
                : Current.TryGetValue(memoryId, out var memory)
                    ? Result<ChatMemory, Problem>.Success(memory)
                    : Result<ChatMemory, Problem>.Failure(new Problem.NotFound("Not a current memory."));

        private static MemoryOrigin Origin(Guid sessionId, MemoryChange change) =>
            new MemoryOrigin.Distilled(sessionId, change.SourceMessageIds);
    }
}
