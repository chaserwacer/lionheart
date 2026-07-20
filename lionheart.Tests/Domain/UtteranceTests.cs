using lionheart.Model.Training.Sets;

namespace lionheart.Tests.Domain;

public class UtteranceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(0.6)]
    [InlineData(1)]
    public void Texted_accepts_confidence_within_zero_to_one(double confidence)
    {
        var texted = new Texted("worked up to 235", confidence);

        Assert.Equal("worked up to 235", texted.RawText);
        Assert.Equal(confidence, texted.StructureConfidence);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Texted_rejects_confidence_outside_zero_to_one(double confidence) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Texted("anything", confidence));
}
