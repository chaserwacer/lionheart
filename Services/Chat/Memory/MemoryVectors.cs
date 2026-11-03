using lionheart.Functional;
using lionheart.Model.Chat.Memory;
using lionheart.Services.Embedding;

namespace lionheart.Services.Chat.Memory;

/// <summary>
/// Embeddings for memory, best-effort throughout.
/// </summary>
/// <remarks>
/// The embedding server is a local process, and memory must keep working without it: a memory
/// written while it is down is kept unembedded and recalled by its words until a later write embeds
/// it, and a query that cannot be embedded is ranked by words alone. So nothing here fails — an
/// empty vector is the answer when there is none.
/// </remarks>
public sealed class MemoryVectors(IEmbeddingGenerator embeddings)
{
    private readonly IEmbeddingGenerator _embeddings = embeddings;

    /// <summary>The text's vector, or an empty one when it cannot be had.</summary>
    public Task<float[]> OfAsync(string text) =>
        string.IsNullOrWhiteSpace(text)
            ? Task.FromResult(Array.Empty<float>())
            : _embeddings.GenerateAsync(text).MatchAsync(vector => vector, _ => []);

    /// <summary>Embeds every one of these that has no vector yet, in one batch.</summary>
    public async Task EmbedAsync(IEnumerable<ChatMemory> memories)
    {
        var unembedded = memories.Where(memory => !memory.IsEmbedded).ToList();
        if (unembedded.Count == 0) return;

        await _embeddings.GenerateBatchAsync([.. unembedded.Select(memory => memory.Text)])
            .TapAsync(vectors =>
            {
                foreach (var (memory, vector) in unembedded.Zip(vectors))
                {
                    memory.Embedded(vector);
                }
            });
    }
}
