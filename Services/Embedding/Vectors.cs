using Ardalis.Result;

namespace lionheart.Services.Embedding;

public static class Vectors
{
    /// <summary>
    /// Cosine similarity in [-1, 1]. Returns <see cref="Result"/> rather than a bare double
    /// because a dimension mismatch is a real deployment condition, not a bug: it means the
    /// stored vector came from a different embedding model and the vocabulary needs a full
    /// recompute. Silently scoring such a pair would corrupt resolution.
    /// </summary>
    public static Result<double> CosineSimilarity(float[] left, float[] right)
    {
        if (left.Length != right.Length)
        {
            return Result<double>.Error(
                $"Embedding dimension mismatch ({left.Length} vs {right.Length}): stored vectors " +
                "predate the current embedding model and must be recomputed.");
        }

        double dot = 0, leftMagnitude = 0, rightMagnitude = 0;
        for (var i = 0; i < left.Length; i++)
        {
            dot += left[i] * right[i];
            leftMagnitude += left[i] * left[i];
            rightMagnitude += right[i] * right[i];
        }

        var denominator = Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude);
        return denominator == 0
            ? Result<double>.Success(0)
            : Result<double>.Success(dot / denominator);
    }
}
