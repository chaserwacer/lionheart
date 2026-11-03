using System.Globalization;
using lionheart.Data;
using lionheart.Model.Chat.Memory;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Model.Chat.Tools;

namespace lionheart.Services.Chat.Memory;

/// <summary>Memory as the chat model reaches it: what a session opens with, and a search for the rest.</summary>
public interface IMemoryRecallService
{
    Task<RecalledMemories> ForSessionAsync(Guid userId, string opening, CancellationToken ct);

    Task<IReadOnlyList<RecalledMemory>> SearchMemoryForChatAsync(IdentityUser user, string query);
}

/// <summary>
/// Reads current memories and ranks them with <see cref="MemoryRecall"/>. Never fails: memory is
/// context, and a chat that cannot recall still answers.
/// </summary>
[ToolProvider]
public sealed class MemoryRecallService(ModelContext context, MemoryVectors vectors) : IMemoryRecallService
{
    private readonly ModelContext _context = context;
    private readonly MemoryVectors _vectors = vectors;

    public async Task<RecalledMemories> ForSessionAsync(Guid userId, string opening, CancellationToken ct)
    {
        var current = await CurrentAsync(userId, ct);
        return current.Count == 0
            ? RecalledMemories.None
            : MemoryRecall.ForSession(current, opening, await _vectors.OfAsync(opening));
    }

    [Tool(Name = "SearchMemory",
        Description = "Search what the athlete has told Lionheart in earlier chat sessions — his goals, injuries, preferences, commitments, decisions and facts about him — by meaning. Returns the current memories that best match the query, each with its kind and the date it was recorded, or nothing when none do. Use it when a question turns on something he may have said before that is not among the memories already in your instructions. It searches what he said, not his training data: for sessions, records and ring data use the other tools.")]
    public async Task<IReadOnlyList<RecalledMemory>> SearchMemoryForChatAsync(IdentityUser user, string query)
    {
        var current = await CurrentAsync(Guid.Parse(user.Id), CancellationToken.None);
        if (current.Count == 0 || string.IsNullOrWhiteSpace(query)) return [];

        return [.. MemoryRecall.Search(current, query, await _vectors.OfAsync(query)).Select(Recalled)];
    }

    public static RecalledMemory Recalled(ChatMemory memory) =>
        new(memory.Kind.Word, memory.RecordedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), memory.Text);

    private Task<List<ChatMemory>> CurrentAsync(Guid userId, CancellationToken ct) =>
        _context.ChatMemories
            .AsNoTracking()
            .Where(memory => memory.UserId == userId
                && EF.Property<bool>(memory, ChatThreadConfiguration.MemoryIsCurrentColumn))
            .ToListAsync(ct);
}
