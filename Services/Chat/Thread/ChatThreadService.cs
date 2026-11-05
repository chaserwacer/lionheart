using System.Diagnostics;
using System.Globalization;
using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Chat;
using lionheart.Model.Chat.Changes;
using lionheart.Model.Chat.Tools;
using lionheart.Services.Chat.Changes;
using lionheart.Model.Chat.Thread;
using lionheart.Services.Chat.Memory;
using lionheart.Services.Chat.Today;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Model.Chat.Completion;
using Services.Chat;

namespace lionheart.Services.Chat.Thread;

/// <summary>The athlete's one continuing chat: read back through, and spoken into.</summary>
public interface IChatThreadService
{
    Task<Result<ChatThreadPage, Problem>> ReadAsync(IdentityUser user, ThreadPage page, CancellationToken ct);

    /// <summary>
    /// Sends one message and runs the whole exchange, telling <paramref name="observer"/> what it
    /// does as it goes. Answers with the session it went into, which is a new one when the thread
    /// had sat idle or grown full.
    /// </summary>
    Task<Result<ThreadSessionView, Problem>> SendAsync(
        IdentityUser user, SendThreadMessageRequest request, IExchangeObserver observer, CancellationToken ct);
}

/// <summary>
/// The thread is a run of sessions, and the model only ever sees the one a message lands in: its
/// frozen system prompt — the card and the memories recalled for it — and its turns. A message that
/// arrives after the idle gap, or into a full session, first has the ended sessions distilled into
/// memory, then opens a new session that starts with what was distilled.
/// </summary>
/// <remarks>
/// Like a standalone conversation, a failed exchange writes nothing: the athlete's message, and a
/// session opened for it, are saved only once the model has answered. Consolidation is the
/// exception, saved on its own before the new session opens, because it is complete whether or not
/// the answer that follows succeeds.
/// </remarks>
public sealed class ChatThreadService(
    ModelContext context,
    IChatCompletionService completion,
    ISessionConsolidator consolidator,
    IMemoryRecallService recall,
    ChatSystemPrompt systemPrompt,
    ITodayBriefing briefing,
    ChatThreadGate gate,
    TimeProvider clock) : IChatThreadService
{
    /// <summary>Sessions per page: a few days of an ordinary week.</summary>
    public const int PageSize = 10;

    private readonly ModelContext _context = context;
    private readonly IChatCompletionService _completion = completion;
    private readonly ISessionConsolidator _consolidator = consolidator;
    private readonly IMemoryRecallService _recall = recall;
    private readonly ChatSystemPrompt _systemPrompt = systemPrompt;
    private readonly ITodayBriefing _briefing = briefing;
    private readonly ChatThreadGate _gate = gate;
    private readonly TimeProvider _clock = clock;

    /// <remarks>Read on the identity's own id, so an account with no profile reads an empty thread.</remarks>
    public async Task<Result<ChatThreadPage, Problem>> ReadAsync(IdentityUser user, ThreadPage page, CancellationToken ct)
    {
        var userId = Guid.Parse(user.Id);
        var sessions = await Within(page, _context.ChatThreadSessions.AsNoTracking().Where(s => s.UserId == userId))
            .OrderByDescending(session => session.OpenedAt)
            .Select(session => session.ChatConversationId)
            .Take(PageSize + 1)
            .ToListAsync(ct);
        var shown = sessions.Take(PageSize).ToList();

        var conversations = await _context.ChatConversations
            .AsNoTracking()
            .Where(conversation => shown.Contains(conversation.ChatConversationID))
            .Include(conversation => conversation.UserMessages)
            .Include(conversation => conversation.ModelMessages)
            .AsSplitQuery()
            .ToListAsync(ct);
        var proposals = await _context.ProposalViewsAsync(shown, ct);

        return Result<ChatThreadPage, Problem>.Success(new ChatThreadPage(
            [.. conversations
                .OrderBy(conversation => conversation.CreatedAt)
                .Select(conversation => ThreadSessionView.Of(conversation, proposals[conversation.ChatConversationID]))],
            HasEarlier: sessions.Count > PageSize));
    }

    public Task<Result<ThreadSessionView, Problem>> SendAsync(
        IdentityUser user, SendThreadMessageRequest request, IExchangeObserver observer, CancellationToken ct) =>
        Words(request.Content)
            .BindAsync(words => _context.AthleteIdAsync(user, ct).MapAsync(userId => new Asked(user, userId, words)))
            .BindAsync(asked => _gate.RunAsync(token => ExchangeAsync(asked, observer, token), ct));

    /// <summary>Find or open the session, answer in it, save.</summary>
    private async Task<Result<ThreadSessionView, Problem>> ExchangeAsync(
        Asked asked, IExchangeObserver observer, CancellationToken ct) =>
        await AnswerAsync(asked, await SessionForAsync(asked, ct), observer, ct)
            .MapAsync(session => CommitAsync(session, ct));

    // ---- Guard ----

    private static Result<string, Problem> Words(string? content) =>
        string.IsNullOrWhiteSpace(content)
            ? Result<string, Problem>.Failure(new Problem.Invalid(nameof(SendThreadMessageRequest.Content), "Say something."))
            : Result<string, Problem>.Success(content.Trim());

    private static IQueryable<ChatThreadSession> Within(ThreadPage page, IQueryable<ChatThreadSession> sessions) =>
        page switch
        {
            ThreadPage.Latest => sessions,
            ThreadPage.Before before => sessions.Where(session => session.OpenedAt < before.OpenedBefore),
            _ => throw new UnreachableException(),
        };

    // ---- The session a message lands in ----

    /// <summary>
    /// The newest session, when the message belongs to it; otherwise a new one, opened after the
    /// ended ones are distilled so that it starts with what they left in memory.
    /// </summary>
    private async Task<LHChatConversation> SessionForAsync(Asked asked, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        switch (SessionBoundary.Decide(await NewestAsync(asked.UserId, ct), now))
        {
            case SessionBoundary.Continues continues:
                return continues.Tip.Conversation;

            case SessionBoundary.Ended:
                // A failed consolidation is recorded on its session and retried later; the athlete's
                // message is not held up for it.
                await _consolidator.ConsolidateEndedAsync(asked.UserId, ct);
                return await OpenAsync(asked, now, ct);

            case SessionBoundary.NoSessionYet:
                return await OpenAsync(asked, now, ct);

            default:
                throw new UnreachableException();
        }
    }

    /// <summary>The thread's newest session, with every turn it holds, tracked so an exchange can add to it.</summary>
    private async Task<SessionTip?> NewestAsync(Guid userId, CancellationToken ct)
    {
        var session = await _context.ChatThreadSessions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.OpenedAt)
            .FirstOrDefaultAsync(ct);
        if (session is null) return null;

        var conversation = await _context.ChatConversations
            .Include(c => c.ChatSystemMessage)
            .Include(c => c.UserMessages)
            .Include(c => c.ModelMessages)
            .Include(c => c.ToolMessages)
            .AsSplitQuery()
            .FirstAsync(c => c.ChatConversationID == session.ChatConversationId, ct);
        return new SessionTip(session, conversation);
    }

    /// <summary>
    /// A new session, staged: a conversation whose system prompt is the card and the memories this
    /// opening message recalls, frozen for the session's life so every turn of it reads one cached prefix.
    /// </summary>
    private async Task<LHChatConversation> OpenAsync(Asked asked, DateTime now, CancellationToken ct)
    {
        var card = await _systemPrompt.ForAsync(asked.User);
        var memories = await _recall.ForSessionAsync(asked.UserId, asked.Words, ct);
        var prompt = ThreadSessionPrompt.Render(card, memories, now, _clock.LocalTimeZone);

        var conversationId = Guid.CreateVersion7(_clock.GetUtcNow());
        var conversation = new LHChatConversation
        {
            ChatConversationID = conversationId,
            UserID = asked.UserId,
            CreatedAt = now,
            LastUpdate = now,
            Name = $"Thread session {now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC",
            ChatSystemMessage = new LHSystemChatMessage
            {
                ChatMessageItemID = Guid.CreateVersion7(_clock.GetUtcNow()),
                ChatConversationID = conversationId,
                CreationTime = now,
                TokenCount = prompt.Length / 4,
                Content = prompt,
            },
            UserMessages = [],
            ModelMessages = [],
            ToolMessages = [],
        };

        _context.ChatConversations.Add(conversation);
        _context.ChatThreadSessions.Add(ChatThreadSession.Open(conversationId, asked.UserId, now));
        return conversation;
    }

    // ---- The exchange ----

    /// <remarks>
    /// The briefing is read once per exchange, before the athlete's words are staged, and goes with
    /// every round of it: the session's prompt stays frozen while the briefing is always now. Where
    /// the changes the model proposed stand goes with it, for the same reason.
    /// <para>
    /// The thread is offered the proposing tools because the phone draws a card for each proposal;
    /// the proposals the exchange makes are staged with its messages, so a failed exchange leaves no
    /// card behind either.
    /// </para>
    /// </remarks>
    private async Task<Result<LHChatConversation, Problem>> AnswerAsync(
        Asked asked, LHChatConversation session, IExchangeObserver observer, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var briefing = Briefed(
            await _briefing.ForAsync(asked.User, ct),
            ProposalsNote.Render(await _context.ProposalsToTellAsync(asked.UserId, session.ChatConversationID, now, ct)));
        var message = new LHUserChatMessage
        {
            ChatMessageItemID = Guid.CreateVersion7(_clock.GetUtcNow()),
            ChatConversationID = session.ChatConversationID,
            CreationTime = now,
            TokenCount = asked.Words.Length / 4,
            Content = asked.Words,
        };
        session.UserMessages.Add(message);
        _context.StageChatMessages([message]);

        return await _completion
            .GenerateChatCompletionAsync(
                asked.User,
                new ChatCompletionRequest
                {
                    Conversation = session,
                    MaxOutputTokens = ChatCompletionRequest.ExchangeOutputTokens,
                    Briefing = briefing,
                    Observer = observer,
                    Tools = ToolAccess.ReadAndPropose,
                },
                ct)
            .MapAsync(response =>
            {
                var answeredAt = _clock.GetUtcNow().UtcDateTime;
                _context.StageChatMessages(response.CompletionGeneratedMessages);
                _context.ChangeProposals.AddRange(response.Proposals.Select(draft =>
                    ChangeProposal.Open(draft, asked.UserId, session.ChatConversationID, answeredAt)));
                session.LastUpdate = answeredAt;
                return session;
            });
    }

    private static string Briefed(string briefing, string proposals) =>
        proposals.Length == 0 ? briefing : $"{briefing.TrimEnd()}\n\n{proposals}";

    private async Task<ThreadSessionView> CommitAsync(LHChatConversation session, CancellationToken ct)
    {
        await _context.SaveChangesAsync(ct);
        var proposals = await _context.ProposalViewsAsync([session.ChatConversationID], ct);
        return ThreadSessionView.Of(session, proposals[session.ChatConversationID]);
    }

    /// <summary>A message on its way in: who sent it, as an identity and as an athlete, and what it says.</summary>
    private sealed record Asked(IdentityUser User, Guid UserId, string Words);
}
