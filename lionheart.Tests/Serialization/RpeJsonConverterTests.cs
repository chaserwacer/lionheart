using System.Text.Json;
using lionheart.Model.Training.Sets;

namespace lionheart.Tests.Serialization;

/// <summary>
/// Rpe's storage contract: rated → bare number, unrated → null. Never an object, never
/// a magic zero.
/// </summary>
public class RpeJsonConverterTests
{
    [Fact]
    public void Rated_serializes_as_a_bare_number()
    {
        Assert.Equal("8.5", JsonSerializer.Serialize(Rpe.Of(8.5)));
    }

    [Fact]
    public void Unrated_serializes_as_null()
    {
        Assert.Equal("null", JsonSerializer.Serialize(Rpe.Unrated));
    }

    [Fact]
    public void Number_deserializes_to_rated()
    {
        Assert.Equal(Rpe.Of(7), JsonSerializer.Deserialize<Rpe>("7"));
    }

    [Fact]
    public void Null_deserializes_to_unrated()
    {
        Assert.Equal(Rpe.Unrated, JsonSerializer.Deserialize<Rpe>("null"));
    }

    [Fact]
    public void Stored_value_outside_range_fails_loudly() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => JsonSerializer.Deserialize<Rpe>("11"));
}
