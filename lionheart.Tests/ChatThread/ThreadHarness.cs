using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Chat;
using lionheart.Model.Chat.Memory;
using lionheart.Model.Chat.Thread;
using lionheart.Model.Profile;
using lionheart.Services.Chat;
using lionheart.Services.Chat.Memory;
using lionheart.Services.Chat.Thread;
using lionheart.Services.Llm;
using lionheart.Services.Profile;
using lionheart.Tests.Persistence;
using lionheart.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Services.Chat;

namespace lionheart.Tests.ChatThread;

/// <summary>
/// The thread wired as the app wires it — real services over a real in-memory database, a canned
/// LM Studio server as the model, a deterministic embedder, and a clock the test moves. Each call
/// gets a fresh context, the way each request gets its own.
/// </summary>
public sealed class ThreadHarness : IDisposable
{
    public ThreadHarness() => Athlete = new IdentityUser { Id = Database.UserId.ToString() };

    public RemodelDatabase Database { get; } = new();

    public FakeChatServer Model { get; } = new();

    public FakeEmbeddingGenerator Embeddings { get; } = new();

    public MovableClock Clock { get; } = new(new DateTimeOffset(Some.Clock));

    public TestToolbox Toolbox { get; } = new();

    public IdentityUser Athlete { get; }

    public ChatThreadGate Gate { get; } = new();

    public Guid AthleteId => Database.UserId;

    public ChatThreadService Thread(ModelContext context)
    {
        var model = Model.CreateChatModel();
        var vectors = new MemoryVectors(Embeddings);
        return new ChatThreadService(
            context,
            new ChatCompletionService(model, Toolbox.Registry, Toolbox.Executor),
            Consolidator(context),
            new MemoryRecallService(context, vectors),
            SystemPrompt,
            Gate,
            Clock);
    }

    /// <summary>The card failing to build, which is its documented fallback: the chat still opens, on the voice alone.</summary>
    public ChatSystemPrompt SystemPrompt { get; } = new(new UnbuildableCard());

    /// <param name="model">The model to consolidate with, when a test needs to act while it is deciding.</param>
    public SessionConsolidator Consolidator(ModelContext context, IChatModel? model = null) =>
        new(context, model ?? Model.CreateChatModel(), new MemoryVectors(Embeddings), Gate, Clock,
            NullLogger<SessionConsolidator>.Instance);

    public ChatMemoryService Memory(ModelContext context) => new(context, new MemoryVectors(Embeddings), Gate, Clock);

    public async Task<ThreadSessionView> SendAsync(string words)
    {
        await using var context = Database.CreateContext();
        return ResultAssert.Succeeded(await Thread(context).SendAsync(
            Athlete, new SendThreadMessageRequest { Content = words }, CancellationToken.None));
    }

    /// <summary>A session already in the thread, as an earlier exchange would have left it.</summary>
    public SeededSession SeedSession(DateTime openedAt, params string[] athleteSaid)
    {
        using var context = Database.CreateContext();
        var conversationId = Guid.NewGuid();
        var said = athleteSaid
            .Select((words, index) => new LHUserChatMessage
            {
                ChatMessageItemID = Guid.NewGuid(),
                ChatConversationID = conversationId,
                CreationTime = openedAt.AddMinutes(index),
                TokenCount = words.Length / 4,
                Content = words,
            })
            .ToList();

        context.ChatConversations.Add(new LHChatConversation
        {
            ChatConversationID = conversationId,
            UserID = AthleteId,
            CreatedAt = openedAt,
            LastUpdate = openedAt.AddMinutes(athleteSaid.Length),
            Name = "seeded",
            ChatSystemMessage = new LHSystemChatMessage
            {
                ChatMessageItemID = Guid.NewGuid(),
                ChatConversationID = conversationId,
                CreationTime = openedAt,
                TokenCount = 1,
                Content = "seeded prompt",
            },
            UserMessages = said,
            ModelMessages = [],
            ToolMessages = [],
        });
        context.ChatThreadSessions.Add(ChatThreadSession.Open(conversationId, AthleteId, openedAt));
        context.SaveChanges();

        return new SeededSession(conversationId, [.. said.Select(message => message.ChatMessageItemID)]);
    }

    public ChatMemory SeedMemory(Func<AMemory, AMemory> shape)
    {
        using var context = Database.CreateContext();
        var memory = shape(AMemory.For(AthleteId)).Build();
        context.ChatMemories.Add(memory);
        context.SaveChanges();
        return memory;
    }

    public List<ChatMemory> Memories()
    {
        using var context = Database.CreateContext();
        return [.. context.ChatMemories.OrderBy(memory => memory.RecordedAt)];
    }

    public ChatThreadSession Session(Guid sessionId)
    {
        using var context = Database.CreateContext();
        return context.ChatThreadSessions.Single(session => session.ChatConversationId == sessionId);
    }

    public string SystemPromptOf(Guid sessionId)
    {
        using var context = Database.CreateContext();
        return context.SystemChatMessages.Single(message => message.ChatConversationID == sessionId).Content!;
    }

    public void Dispose()
    {
        Database.Dispose();
        Model.Dispose();
        Toolbox.Dispose();
        Gate.Dispose();
    }

    private sealed class UnbuildableCard : IAthleteContextCardService
    {
        public Task<Result<AthleteContextCard, Problem>> GetOrBuildCardAsync(IdentityUser user) => Unbuilt();

        public Task<Result<AthleteContextCard, Problem>> RebuildStableProfileAsync(IdentityUser user) => Unbuilt();

        public Task<Result<AthleteContextCard, Problem>> RefreshRecentStateAsync(IdentityUser user) => Unbuilt();

        public string RenderSystemMessage(AthleteContextCard card) => string.Empty;

        private static Task<Result<AthleteContextCard, Problem>> Unbuilt() =>
            Task.FromResult(Result<AthleteContextCard, Problem>.Failure(new Problem.NotFound("No card in tests.")));
    }
}

public sealed record SeededSession(Guid SessionId, IReadOnlyList<Guid> MessageIds);

/// <summary>
/// The canned model, with something done while it is deciding — the window in which the athlete
/// can change memory under a consolidation.
/// </summary>
public sealed class MeanwhileChatModel(IChatModel inner, Func<Task> meanwhile) : IChatModel
{
    public async Task<lionheart.Functional.Result<ModelTurn, Problem>> RespondAsync(
        ChatTurnRequest request, CancellationToken cancellationToken = default)
    {
        await meanwhile();
        return await inner.RespondAsync(request, cancellationToken);
    }
}

/// <summary>A clock the test moves forward, so the idle gap can pass without waiting for it.</summary>
public sealed class MovableClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public void Advance(TimeSpan by) => _now += by;
}
