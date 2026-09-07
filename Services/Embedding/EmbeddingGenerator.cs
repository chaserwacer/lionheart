using lionheart.Functional;
using OpenAI.Embeddings;

namespace lionheart.Services.Embedding;

/// <summary>
/// Produces the vectors behind semantic facet resolution and (later) retrieval ranking.
/// Vectors from different embedding models are mutually incomparable, so a model swap
/// invalidates everything already stored — see <see cref="Vectors.CosineSimilarity"/>.
/// </summary>
public interface IEmbeddingGenerator
{
    Task<Result<float[], Problem>> GenerateAsync(string text);
    Task<Result<IReadOnlyList<float[]>, Problem>> GenerateBatchAsync(IReadOnlyList<string> texts);
}

/// <summary>
/// Embeddings from LM Studio's OpenAI-compatible endpoint — the same locally hosted
/// server that serves chat, so ingestion needs no external service.
/// </summary>
public sealed class LmStudioEmbeddingGenerator : IEmbeddingGenerator
{
    private readonly EmbeddingClient _client;

    public LmStudioEmbeddingGenerator(EmbeddingClient client) => _client = client;

    public async Task<Result<float[], Problem>> GenerateAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result<float[], Problem>.Failure(
                new Problem.Invalid(nameof(text), "Cannot embed empty text."));
        }

        try
        {
            var embedding = await _client.GenerateEmbeddingAsync(text);
            return Result<float[], Problem>.Success(embedding.Value.ToFloats().ToArray());
        }
        catch (Exception ex)
        {
            return Result<float[], Problem>.Failure(
                new Problem.Upstream($"Embedding generation failed: {ex.Message}"));
        }
    }

    public async Task<Result<IReadOnlyList<float[]>, Problem>> GenerateBatchAsync(IReadOnlyList<string> texts)
    {
        if (texts.Count == 0) return Result<IReadOnlyList<float[]>, Problem>.Success([]);

        if (texts.Any(string.IsNullOrWhiteSpace))
        {
            return Result<IReadOnlyList<float[]>, Problem>.Failure(
                new Problem.Invalid(nameof(texts), "Cannot embed empty text."));
        }

        try
        {
            var embeddings = await _client.GenerateEmbeddingsAsync(texts);
            IReadOnlyList<float[]> vectors = [.. embeddings.Value.Select(e => e.ToFloats().ToArray())];
            return Result<IReadOnlyList<float[]>, Problem>.Success(vectors);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<float[]>, Problem>.Failure(
                new Problem.Upstream($"Embedding generation failed: {ex.Message}"));
        }
    }
}
