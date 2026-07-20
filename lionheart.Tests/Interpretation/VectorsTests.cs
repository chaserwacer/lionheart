using lionheart.Services.Embedding;

namespace lionheart.Tests.Interpretation;

public class VectorsTests
{
    [Fact]
    public void IdenticalVectorsScoreOne()
    {
        var similarity = Vectors.CosineSimilarity([1f, 2f, 3f], [1f, 2f, 3f]);

        Assert.True(similarity.IsSuccess);
        Assert.Equal(1.0, similarity.Value, precision: 6);
    }

    [Fact]
    public void OrthogonalVectorsScoreZero()
    {
        var similarity = Vectors.CosineSimilarity([1f, 0f], [0f, 1f]);

        Assert.True(similarity.IsSuccess);
        Assert.Equal(0.0, similarity.Value, precision: 6);
    }

    [Fact]
    public void MagnitudeDoesNotAffectScore()
    {
        var similarity = Vectors.CosineSimilarity([1f, 1f], [10f, 10f]);

        Assert.True(similarity.IsSuccess);
        Assert.Equal(1.0, similarity.Value, precision: 6);
    }

    [Fact]
    public void ZeroVectorScoresZeroRatherThanDividingByZero()
    {
        var similarity = Vectors.CosineSimilarity([0f, 0f], [1f, 1f]);

        Assert.True(similarity.IsSuccess);
        Assert.Equal(0.0, similarity.Value);
    }

    /// <summary>
    /// A dimension mismatch means the stored vector predates the current embedding model.
    /// Scoring it anyway would silently corrupt resolution, so it has to surface as an error.
    /// </summary>
    [Fact]
    public void DimensionMismatchIsAnErrorNamingTheRecompute()
    {
        var similarity = Vectors.CosineSimilarity([1f, 0f, 0f], [1f, 0f]);

        Assert.False(similarity.IsSuccess);
        Assert.Contains("recomputed", string.Join(" ", similarity.Errors));
    }
}
