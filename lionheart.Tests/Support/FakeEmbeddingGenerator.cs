using Ardalis.Result;
using lionheart.Services.Embedding;

namespace lionheart.Tests.Support;

/// <summary>
/// Deterministic stand-in for the LM Studio embedder. Unregistered text gets a one-hot vector
/// on a hash-chosen dimension, so distinct strings are orthogonal and never snap to each other
/// by accident; tests register explicit vectors to stage the near-matches they care about.
/// </summary>
public sealed class FakeEmbeddingGenerator : IEmbeddingGenerator
{
    public const int Dimensions = 64;

    private readonly Dictionary<string, float[]> _registered = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Requested { get; } = [];

    public string? FailWith { get; set; }

    public FakeEmbeddingGenerator Returns(string text, params float[] vector)
    {
        _registered[text] = vector;
        return this;
    }

    public Task<Result<float[]>> GenerateAsync(string text)
    {
        Requested.Add(text);
        return Task.FromResult(
            FailWith is null ? Result<float[]>.Success(VectorFor(text)) : Result<float[]>.Error(FailWith));
    }

    public Task<Result<IReadOnlyList<float[]>>> GenerateBatchAsync(IReadOnlyList<string> texts)
    {
        Requested.AddRange(texts);
        if (FailWith is not null) return Task.FromResult(Result<IReadOnlyList<float[]>>.Error(FailWith));

        IReadOnlyList<float[]> vectors = [.. texts.Select(VectorFor)];
        return Task.FromResult(Result<IReadOnlyList<float[]>>.Success(vectors));
    }

    private float[] VectorFor(string text)
    {
        if (_registered.TryGetValue(text, out var registered)) return registered;

        var vector = new float[Dimensions];
        vector[Math.Abs(text.ToLowerInvariant().GetHashCode()) % Dimensions] = 1f;
        return vector;
    }

    /// <summary>A unit vector pointing along one axis — the shape unregistered text produces.</summary>
    public static float[] OneHot(int dimension)
    {
        var vector = new float[Dimensions];
        vector[dimension] = 1f;
        return vector;
    }

    /// <summary>
    /// Two axes mixed by <paramref name="tilt"/>, for staging a pair whose cosine sits either
    /// side of the resolver's snap threshold.
    /// </summary>
    public static float[] Between(int dimension, int other, float tilt)
    {
        var vector = new float[Dimensions];
        vector[dimension] = 1f - tilt;
        vector[other] = tilt;
        return vector;
    }
}
