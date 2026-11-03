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
/// change the model got wrong — a handle that names nothing, two changes to one memory, a memory
/// the athlete changed while the model was deciding — is set aside, logged, and the rest applied.
/// Each memory is dated by the latest message it rests on, not by when the session was distilled.
/// </para>
/// </remarks>
public sealed class SessionConsolidator(
    ModelContext context,
    IChatModel model,
    MemoryVectors vectors,
    ChatThreadGate gate,
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
    private readonly ChatThreadGate _gate = gate;
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
    /// Read, ask, then apply under the memory lock. A failure anywhere is saved instead, as the
    /// record that this attempt failed.
    /// </summary>
    private async Task<Result<ChatThreadSession, Problem>> ConsolidateAsync(
        ChatThreadSession session, CancellationToken ct)
    {
        var transcript = await TranscriptAsync(session, ct);
        var current = await CurrentAsync(session.UserId, ct);
        var prompt = ConsolidationPrompt.For(
            session.OpenedAt, transcript, await CandidatesAsync(current, transcript), _clock.LocalTimeZone);

        return await PlanAsync(prompt, transcript, ct)
            .TapAsync(plan => Report(session, plan.Unusable))
            .BindAsync(plan => _gate.WriteMemoryAsync(
                token => ApplyAsync(session, plan, current, Said(transcript), token), ct))
            .TapErrorAsync(problem => RecordFailureAsync(session, problem, ct))
            .MapAsync(_ => session);
    }

    /// <summary>
    /// The plan applied to memory as it stands now, not as it stood when the model was asked: the
    /// athlete may have corrected or forgotten a memory while the model was deciding, and a change
    /// to a memory he has since replaced is set aside rather than written over his.
    /// </summary>
    private async Task<Result<Staged, Problem>> ApplyAsync(
        ChatThreadSession session, ConsolidationPlan plan, IReadOnlyList<ChatMemory> asked,
        IReadOnlyDictionary<Guid, DateTime> said, CancellationToken ct)
    {
        var now = _clock.GetUtcNow();
        var staged = plan.Changes.Aggregate(
            Staged.Over(session.UserId, await StillCurrentAsync(asked, ct), said, session.OpenedAt),
            (staged, change) => staged.Apply(change, session.ChatConversationId, now));
        Report(session, staged.SetAside);

        return await session
            .Consolidated(now.UtcDateTime, staged.Remembered, staged.Revised, staged.Retired)
            .MapAsync(_ => KeepAsync(staged, ct));
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

    /// <summary>When each message of the session was said, by id.</summary>
    private static IReadOnlyDictionary<Guid, DateTime> Said(IReadOnlyList<LHChatMessage> transcript) =>
        transcript.ToDictionary(message => message.ChatMessageItemID, message => message.CreationTime);

    /// <summary>Tracked, because a consolidation supersedes and retires them.</summary>
    private Task<List<ChatMemory>> CurrentAsync(Guid userId, CancellationToken ct) =>
        _context.ChatMemories
            .Where(memory => memory.UserId == userId
                && EF.Property<bool>(memory, ChatThreadConfiguration.MemoryIsCurrentColumn))
            .ToListAsync(ct);

    /// <summary>
    /// The memories the model was shown, read again: one forgotten since is gone, and one replaced
    /// since is no longer current.
    /// </summary>
    private async Task<IReadOnlyList<ChatMemory>> StillCurrentAsync(IReadOnlyList<ChatMemory> asked, CancellationToken ct)
    {
        foreach (var memory in asked)
        {
            await _context.Entry(memory).ReloadAsync(ct);
        }

        return [.. asked.Where(memory => _context.Entry(memory).State is not EntityState.Detached && memory.IsCurrent)];
    }

    private async Task<IReadOnlyList<ChatMemory>> CandidatesAsync(
        IReadOnlyList<ChatMemory> current, IReadOnlyList<LHChatMessage> transcript)
    {
        var said = string.Join("\n", transcript.OfType<LHUserChatMessage>().Select(message => message.Content));
        var vector = current.Count > MemoryRecall.ConsolidationLimit ? await _vectors.OfAsync(said) : [];
        return MemoryRecall.ForConsolidation(current, said, vector);
    }

    // ---- Asking ----

    /// <summary>A session with nothing said in it is consolidated with no changes, without a model call.</summary>
    private Task<Result<ConsolidationPlan, Problem>> PlanAsync(
        ConsolidationPrompt prompt, IReadOnlyList<LHChatMessage> transcript, CancellationToken ct) =>
        transcript.Count == 0
            ? Task.FromResult(Result<ConsolidationPlan, Problem>.Success(new ConsolidationPlan([], [])))
            : _model
                .RespondAsync(
                    ChatTurnRequest.Extraction(
                        prompt.SystemPrompt, prompt.UserText, ConsolidationPrompt.SchemaName, ConsolidationPrompt.Schema,
                        ConsolidationTokenBudget),
                    ct)
                .BindAsync(turn => ConsolidationParser.Read(turn, prompt));

    private void Report(ChatThreadSession session, IReadOnlyList<string> setAside)
    {
        foreach (var reason in setAside)
        {
            _logger.LogWarning(
                "Consolidating session {SessionId} set aside a change: {Reason}", session.ChatConversationId, reason);
        }
    }

    // ---- Writing ----

    /// <summary>Embeds what is new, and anything current still waiting for a vector, then saves the consolidation.</summary>
    private async Task<Staged> KeepAsync(Staged staged, CancellationToken ct)
    {
        await _vectors.EmbedAsync(staged.Current.Values.Where(memory => memory.IsCurrent).Concat(staged.Added));
        _context.ChatMemories.AddRange(staged.Added);
        await _context.SaveChangesAsync(ct);
        return staged;
    }

    /// <summary>
    /// Undoes whatever this attempt did to memory and saves the failure on the session, and nothing
    /// else.
    /// </summary>
    private async Task RecordFailureAsync(ChatThreadSession session, Problem problem, CancellationToken ct)
    {
        foreach (var entry in _context.ChangeTracker.Entries<ChatMemory>().Where(entry => entry.State is EntityState.Modified))
        {
            entry.CurrentValues.SetValues(entry.OriginalValues);
            entry.State = EntityState.Unchanged;
        }

        session.ConsolidationFailed(_clock.GetUtcNow().UtcDateTime, problem.Description);
        await _context.SaveChangesAsync(ct);
        _logger.LogWarning(
            "Consolidating session {SessionId} failed: {Reason}", session.ChatConversationId, problem.Description);
    }

    /// <summary>
    /// The memory a plan is building: what it added, which memories it has changed, what it set
    /// aside and why, and the tally.
    /// </summary>
    /// <param name="Said">When each message of the session was said, so a memory is dated by the words it rests on.</param>
    /// <param name="Opened">The session's opening, the date of a memory that names no message.</param>
    private sealed record Staged(
        Guid UserId,
        IReadOnlyDictionary<Guid, ChatMemory> Current,
        IReadOnlyDictionary<Guid, DateTime> Said,
        DateTime Opened,
        IReadOnlyList<ChatMemory> Added,
        IReadOnlySet<Guid> Changed,
        IReadOnlyList<string> SetAside,
        int Remembered,
        int Revised,
        int Retired)
    {
        public static Staged Over(
            Guid userId, IReadOnlyList<ChatMemory> current, IReadOnlyDictionary<Guid, DateTime> said, DateTime opened) =>
            new(userId, current.ToDictionary(memory => memory.MemoryId), said, opened, [], new HashSet<Guid>(), [], 0, 0, 0);

        /// <summary>
        /// One change applied, or set aside with the domain's reason — a total step of the fold, so
        /// one refused change never stops the rest.
        /// </summary>
        public Staged Apply(MemoryChange change, Guid sessionId, DateTimeOffset now) =>
            change switch
            {
                MemoryChange.Remember remember => Repeats(remember.Text)
                    ? this
                    : ChatMemory
                        .Record(Guid.CreateVersion7(now), UserId, remember.Kind, remember.Text,
                            Origin(sessionId, change), SaidAt(change))
                        .Match(memory => this with { Added = [.. Added, memory], Remembered = Remembered + 1 }, Aside),

                MemoryChange.Revise revise => Target(revise.MemoryId)
                    .Bind(memory => memory.Revise(
                        Guid.CreateVersion7(now), revise.Kind, revise.Text, Origin(sessionId, change), SaidAt(change)))
                    .Match(
                        successor => this with
                        {
                            Added = [.. Added, successor],
                            Changed = new HashSet<Guid>(Changed) { revise.MemoryId },
                            Revised = Revised + 1,
                        },
                        Aside),

                MemoryChange.Retire retire => Target(retire.MemoryId)
                    .Bind(memory => memory.Retire(SaidAt(change), retire.Reason))
                    .Match(
                        _ => this with { Changed = new HashSet<Guid>(Changed) { retire.MemoryId }, Retired = Retired + 1 },
                        Aside),

                _ => throw new UnreachableException(),
            };

        private Staged Aside(Problem problem) => this with { SetAside = [.. SetAside, problem.Description] };

        /// <summary>When the latest of the messages a change cites was said, or the session's opening.</summary>
        private DateTime SaidAt(MemoryChange change) =>
            change.SourceMessageIds
                .Where(Said.ContainsKey)
                .Select(id => Said[id])
                .DefaultIfEmpty(Opened)
                .Max();

        /// <summary>Whether a memory already kept, or added by this plan, says the same thing.</summary>
        private bool Repeats(string text) =>
            Current.Values.Where(memory => memory.IsCurrent).Concat(Added)
                .Any(memory => string.Equals(memory.Text, text.Trim(), StringComparison.OrdinalIgnoreCase));

        private Result<ChatMemory, Problem> Target(Guid memoryId) =>
            Changed.Contains(memoryId)
                ? Result<ChatMemory, Problem>.Failure(new Problem.Conflict("Already changed by this consolidation."))
                : Current.TryGetValue(memoryId, out var memory)
                    ? Result<ChatMemory, Problem>.Success(memory)
                    : Result<ChatMemory, Problem>.Failure(new Problem.NotFound(
                        "Not a current memory: replaced or forgotten since the model was asked."));

        private static MemoryOrigin Origin(Guid sessionId, MemoryChange change) =>
            new MemoryOrigin.Distilled(sessionId, change.SourceMessageIds);
    }
}
