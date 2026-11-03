namespace lionheart.Tests.Support;

/// <summary>A clock stopped at one instant, so a stamped time can be asserted exactly.</summary>
public sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
