using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Chat;
using lionheart.Model.Chat.Changes;
using lionheart.Services;
using lionheart.Services.Chat.Changes;
using lionheart.Services.Training.Availability;
using lionheart.Services.Training.Focus;
using lionheart.Services.Training.Patterns;
using lionheart.Services.Training.PersonalRecords;
using lionheart.Services.Training.Sessions;
using lionheart.Tests.Api;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.ChatChanges;

/// <summary>
/// The chat's change path wired as the app wires it, over two real athletes: the drafter a tool
/// call reaches, and the proposal service a card's answer reaches, each over a fresh context the way
/// each request gets its own.
/// </summary>
public sealed class ChangeDesk : IDisposable
{
    /// <summary>A Wednesday, mid-afternoon in UTC, so it is the same Wednesday on any American clock.</summary>
    public static readonly DateTimeOffset Now = new(2026, 11, 4, 17, 0, 0, TimeSpan.Zero);

    public static readonly DateOnly Today = new(2026, 11, 4);

    public static readonly FixedClock Clock = new(Now);

    public TwoAthletes Athletes { get; } = new();

    public void Dispose() => Athletes.Dispose();

    public async Task<Result<ProposalDraft, Problem>> DraftAsync(
        Func<ChangeDrafter, Task<Result<ProposalDraft, Problem>>> draft)
    {
        await using var context = Athletes.CreateContext();
        return await draft(new ChangeDrafter(context, Clock, MovementPatternMap.Shipped));
    }

    public async Task<T> ProposalsAsync<T>(Func<IChangeProposalService, Task<T>> act)
    {
        await using var context = Athletes.CreateContext();
        return await act(new ChangeProposalService(context, Writer(context), Clock));
    }

    /// <summary>A draft as an exchange in one of the owner's thread sessions would have stored it.</summary>
    public async Task<Guid> StoreAsync(ProposalDraft draft)
    {
        var conversationId = await ConversationAsync();
        await Athletes.PlantAsync(context =>
            context.ChangeProposals.Add(ChangeProposal.Open(draft, Athletes.OwnerId, conversationId, Now.UtcDateTime)));
        return draft.ChangeProposalId;
    }

    /// <summary>Drafts with the owner and stores what was drafted.</summary>
    public async Task<Guid> ProposeAsync(Func<ChangeDrafter, Task<Result<ProposalDraft, Problem>>> draft) =>
        await StoreAsync(ResultAssert.Succeeded(await DraftAsync(draft)));

    public async Task<T> ReadAsync<T>(Func<ModelContext, Task<T>> read)
    {
        await using var context = Athletes.CreateContext();
        return await read(context);
    }

    public Task<int> SessionsStoredAsync() => ReadAsync(context => context.Sessions.CountAsync());

    private static ChangeWriter Writer(ModelContext context)
    {
        var detector = new PersonalRecordDetector(context, Clock);
        return new ChangeWriter(
            context,
            new TrainingSessionService(context, Clock, detector, new PersonalRecordRebuild(context, detector)),
            new NonTrainingDayService(context, Clock),
            new FocusPinService(context, Clock, MovementPatternMap.Shipped),
            new InjuryService(context));
    }

    private async Task<Guid> ConversationAsync()
    {
        var conversationId = Guid.NewGuid();
        await Athletes.PlantAsync(context => context.ChatConversations.Add(new LHChatConversation
        {
            ChatConversationID = conversationId,
            UserID = Athletes.OwnerId,
            CreatedAt = Now.UtcDateTime,
            LastUpdate = Now.UtcDateTime,
            Name = "a thread session",
            ChatSystemMessage = new LHSystemChatMessage
            {
                ChatMessageItemID = Guid.NewGuid(),
                ChatConversationID = conversationId,
                CreationTime = Now.UtcDateTime,
                TokenCount = 1,
                Content = "prompt",
            },
            UserMessages = [],
            ModelMessages = [],
            ToolMessages = [],
        }));
        return conversationId;
    }
}
