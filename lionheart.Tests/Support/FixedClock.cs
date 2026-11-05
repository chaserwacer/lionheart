namespace lionheart.Tests.Support;

/// <summary>
/// A clock stopped at one instant, so a stamped time can be asserted exactly; given a zone, the
/// athlete's local day is pinned too rather than following the machine's.
/// </summary>
public sealed class FixedClock(DateTimeOffset now, TimeZoneInfo zone) : TimeProvider
{
    public FixedClock(DateTimeOffset now) : this(now, TimeZoneInfo.Local) { }

    public override DateTimeOffset GetUtcNow() => now;

    public override TimeZoneInfo LocalTimeZone => zone;
}
