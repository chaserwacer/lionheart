using System.Diagnostics;
using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Chat;
using lionheart.Model.Chat.Memory;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Chat.Memory;

/// <summary>What Lionheart remembers from the chat, in the athlete's hands: read, corrected, deleted.</summary>
public interface IChatMemoryService
{
    /// <summary>Every current memory: the standing kinds first, then the newest first.</summary>
    Task<Result<IReadOnlyList<MemoryView>, Problem>> GetCurrentAsync(IdentityUser user, CancellationToken ct);

    /// <summary>Replaces a current memory with the athlete's own wording. The old version stays as history.</summary>
    Task<Result<MemoryView, Problem>> CorrectAsync(
        IdentityUser user, Guid memoryId, CorrectMemoryRequest request, CancellationToken ct);

    /// <summary>Forgets a memory: every version of it, so nothing of it reaches a session again.</summary>
    Task<Result<Unit, Problem>> DeleteAsync(IdentityUser user, Guid memoryId, CancellationToken ct);
}

/// <summary>
/// The athlete-facing side of memory. Another athlete's memory is a 404, the rule every owned
/// resource here follows.
/// </summary>
public sealed class ChatMemoryService(ModelContext context, MemoryVectors vectors, TimeProvider clock) : IChatMemoryService
{
    /// <summary>A quote is a reminder of what was said, not the message again.</summary>
    private const int QuoteLength = 280;

    private readonly ModelContext _context = context;
    private readonly MemoryVectors _vectors = vectors;
    private readonly TimeProvider _clock = clock;

    public async Task<Result<IReadOnlyList<MemoryView>, Problem>> GetCurrentAsync(IdentityUser user, CancellationToken ct)
    {
        var userId = Guid.Parse(user.Id);
        var memories = await _context.ChatMemories
            .AsNoTracking()
            .Where(memory => memory.UserId == userId)
            .ToListAsync(ct);
        var quotes = await QuotesAsync(memories.Where(memory => memory.IsCurrent), ct);

        IReadOnlyList<MemoryView> views =
        [
            .. memories
                .Where(memory => memory.IsCurrent)
                .OrderByDescending(memory => memory.Kind.IsCore)
                .ThenByDescending(memory => memory.RecordedAt)
                .Select(memory => View(memory, memories, quotes)),
        ];
        return Result<IReadOnlyList<MemoryView>, Problem>.Success(views);
    }

    public Task<Result<MemoryView, Problem>> CorrectAsync(
        IdentityUser user, Guid memoryId, CorrectMemoryRequest request, CancellationToken ct) =>
        OwnedAsync(user, memoryId, ct)
            .BindAsync(memory => Corrected(memory, request))
            .BindAsync(successor => CommitAsync(user, successor, ct));

    public Task<Result<Unit, Problem>> DeleteAsync(IdentityUser user, Guid memoryId, CancellationToken ct) =>
        OwnedAsync(user, memoryId, ct).BindAsync(memory => ForgetAsync(memory, ct));

    // ---- Guard ----

    private async Task<Result<ChatMemory, Problem>> OwnedAsync(IdentityUser user, Guid memoryId, CancellationToken ct)
    {
        var userId = Guid.Parse(user.Id);
        var memory = await _context.ChatMemories
            .FirstOrDefaultAsync(m => m.MemoryId == memoryId && m.UserId == userId, ct);

        return memory is null
            ? Result<ChatMemory, Problem>.Failure(new Problem.NotFound("Memory not found."))
            : Result<ChatMemory, Problem>.Success(memory);
    }

    // ---- Stage ----

    private Result<ChatMemory, Problem> Corrected(ChatMemory memory, CorrectMemoryRequest request)
    {
        var now = _clock.GetUtcNow();
        return memory.Revise(Guid.CreateVersion7(now), request.Kind, request.Text, new MemoryOrigin.Corrected(), now.UtcDateTime);
    }

    // ---- Commit ----

    private async Task<Result<MemoryView, Problem>> CommitAsync(IdentityUser user, ChatMemory successor, CancellationToken ct)
    {
        await _vectors.EmbedAsync([successor]);
        _context.ChatMemories.Add(successor);
        await _context.SaveChangesAsync(ct);

        var lineage = await _context.ChatMemories
            .AsNoTracking()
            .Where(memory => memory.LineageId == successor.LineageId)
            .ToListAsync(ct);
        return Result<MemoryView, Problem>.Success(View(successor, lineage, new Dictionary<Guid, MemoryQuote>()));
    }

    private async Task<Result<Unit, Problem>> ForgetAsync(ChatMemory memory, CancellationToken ct)
    {
        var lineage = await _context.ChatMemories.Where(m => m.LineageId == memory.LineageId).ToListAsync(ct);
        _context.ChatMemories.RemoveRange(lineage);
        await _context.SaveChangesAsync(ct);
        return Result<Unit, Problem>.Success(Unit.Value);
    }

    // ---- Reading ----

    /// <summary>The messages the memories were drawn from, by id.</summary>
    private async Task<IReadOnlyDictionary<Guid, MemoryQuote>> QuotesAsync(IEnumerable<ChatMemory> memories, CancellationToken ct)
    {
        var ids = memories
            .SelectMany(memory => memory.Origin is MemoryOrigin.Distilled distilled ? distilled.SourceMessageIds : [])
            .ToHashSet();
        if (ids.Count == 0) return new Dictionary<Guid, MemoryQuote>();

        var athlete = await _context.UserChatMessages.AsNoTracking()
            .Where(message => ids.Contains(message.ChatMessageItemID))
            .Select(message => new { message.ChatMessageItemID, message.Content, message.CreationTime })
            .ToListAsync(ct);
        var model = await _context.ModelChatMessages.AsNoTracking()
            .Where(message => ids.Contains(message.ChatMessageItemID))
            .Select(message => new { message.ChatMessageItemID, message.Content, message.CreationTime })
            .ToListAsync(ct);

        return athlete.Select(m => Quote(m.ChatMessageItemID, true, m.Content, m.CreationTime))
            .Concat(model.Select(m => Quote(m.ChatMessageItemID, false, m.Content, m.CreationTime)))
            .ToDictionary(quote => quote.MessageId);
    }

    private static MemoryQuote Quote(Guid id, bool byAthlete, string? content, DateTime at)
    {
        var said = (content ?? string.Empty).Trim();
        return new MemoryQuote(id, byAthlete, said.Length <= QuoteLength ? said : said[..QuoteLength] + "…", at);
    }

    /// <summary>A memory with its quotes and the versions before it, read off everything already loaded.</summary>
    private static MemoryView View(
        ChatMemory memory, IReadOnlyList<ChatMemory> loaded, IReadOnlyDictionary<Guid, MemoryQuote> quotes) =>
        new(
            memory.MemoryId,
            memory.Kind,
            memory.Text,
            memory.RecordedAt,
            memory.Origin switch
            {
                MemoryOrigin.Distilled => MemorySource.Chat,
                MemoryOrigin.Corrected => MemorySource.Athlete,
                _ => throw new UnreachableException(),
            },
            memory.Origin is MemoryOrigin.Distilled distilled
                ? [.. distilled.SourceMessageIds
                    .Where(quotes.ContainsKey)
                    .Select(id => quotes[id])
                    .OrderBy(quote => quote.At)]
                : [],
            [
                .. loaded
                    .Where(version => version.LineageId == memory.LineageId)
                    .SelectMany(version => version.Standing is MemoryStanding.Superseded superseded
                        ? [new MemoryVersionView(version.Kind, version.Text, version.RecordedAt, superseded.At)]
                        : Array.Empty<MemoryVersionView>())
                    .OrderByDescending(version => version.RecordedAt),
            ]);
}
