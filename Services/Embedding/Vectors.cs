using lionheart.Functional;

namespace lionheart.Services.Embedding;

public static class Vectors
{
    /// <summary>
    /// Cosine similarity in [-1, 1]. Returns a <see cref="Result{T, TProblem}"/> rather than a bare double
    /// because a dimension mismatch is a real deployment condition, not a bug: it means the
    /// stored vector came from a different embedding model and the vocabulary needs a full
    /// recompute. Silently scoring such a pair would corrupt resolution.
    /// </summary>
    public static Result<double, Problem> CosineSimilarity(float[] left, float[] right)
    {
        if (left.Length != right.Length)
        {
            return Result<double, Problem>.Failure(new Problem.Upstream(
                $"Embedding dimension mismatch ({left.Length} vs {right.Length}): stored vectors " +
                "predate the current embedding model and must be recomputed."));
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
            ? Result<double, Problem>.Success(0)
            : Result<double, Problem>.Success(dot / denominator);
    }
}
