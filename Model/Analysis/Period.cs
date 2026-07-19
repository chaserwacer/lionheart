namespace lionheart.Model.Analysis;

/// <summary>
/// Temporal shape shared by everything analyzable: a single moment or a span.
/// </summary>
public abstract record Period
{
    /// <summary>Where this period begins on the timeline — the uniform sort/join key for retrieval.</summary>
    public abstract DateTime StartTime { get; }
}

public sealed record Instant(DateTime At) : Period
{
    public override DateTime StartTime => At;
}

public sealed record Interval : Period
{
    public Interval(DateTime start, DateTime end)
    {
        if (end < start) throw new ArgumentException("Interval end precedes start.", nameof(end));
        Start = start;
        End = end;
    }

    public DateTime Start { get; }
    public DateTime End { get; }
    public TimeSpan Duration => End - Start;
    public override DateTime StartTime => Start;
}
