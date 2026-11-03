using lionheart.Model.Analysis;

namespace lionheart.Tests.Domain;

public class PeriodTests
{
    private static readonly DateTime T0 = new(2026, 7, 18, 9, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Instant_starts_at_its_moment()
    {
        Assert.Equal(T0, new Instant(T0).StartTime);
    }

    [Fact]
    public void Interval_starts_at_its_start()
    {
        Assert.Equal(T0, new Interval(T0, T0.AddHours(1)).StartTime);
    }

    [Fact]
    public void Interval_duration_is_end_minus_start()
    {
        Assert.Equal(TimeSpan.FromMinutes(75), new Interval(T0, T0.AddMinutes(75)).Duration);
    }

    [Fact]
    public void Interval_may_be_zero_length()
    {
        Assert.Equal(TimeSpan.Zero, new Interval(T0, T0).Duration);
    }

    [Fact]
    public void Interval_rejects_end_before_start() =>
        Assert.Throws<ArgumentException>(() => new Interval(T0, T0.AddTicks(-1)));
}
