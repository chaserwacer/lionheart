using lionheart.Model.Training.Sets;

namespace lionheart.Tests.Domain;

public class RpeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(8.5)]
    [InlineData(10)]
    public void Of_accepts_values_within_zero_to_ten(double value)
    {
        var rpe = Rpe.Of(value);

        Assert.True(rpe.IsRated);
        Assert.Equal(value, rpe.Value);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(10.1)]
    [InlineData(double.NaN)]
    public void Of_rejects_values_outside_zero_to_ten(double value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Rpe.Of(value));

    [Fact]
    public void Unrated_is_not_rated()
    {
        Assert.False(Rpe.Unrated.IsRated);
    }

    [Fact]
    public void Unrated_refuses_to_yield_a_value() =>
        Assert.Throws<InvalidOperationException>(() => Rpe.Unrated.Value);

    [Fact]
    public void Default_struct_value_is_unrated()
    {
        Assert.Equal(Rpe.Unrated, default);
    }

    [Fact]
    public void Equal_ratings_are_equal()
    {
        Assert.Equal(Rpe.Of(8), Rpe.Of(8));
        Assert.NotEqual(Rpe.Of(8), Rpe.Of(9));
        Assert.NotEqual(Rpe.Of(0), Rpe.Unrated);
    }
}
