using System.Text.Json.Serialization;

namespace lionheart.Model.Analysis;

/// <summary>
/// Temporal shape shared by everything analyzable: a single moment or a span.
/// </summary>
/// <remarks>
/// Held as <see cref="DateTimeOffset"/> so the offset in force when the entry was logged travels
/// with it. Two different questions get asked of the same moment and each needs a different half:
/// retrieval orders and windows by <see cref="StartTimeUtc"/> (one absolute timeline, which is
/// what the stamped StartTime column indexes), while the athlete asks what *day* it was, which is
/// only answerable against the offset they were living in — see <see cref="LocalDate"/>. Deriving
/// the day from UTC instead would move an evening session to tomorrow anywhere west of Greenwich.
/// Capturing the offset rather than a time zone id is deliberate: for an event that already
/// happened, the offset that applied at that instant is the whole answer, and it stays correct
/// across travel and DST without a rules lookup.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Instant), "instant")]
[JsonDerivedType(typeof(Interval), "interval")]
public abstract record Period
{
    /// <summary>
    /// Where this period begins, offset included.
    /// </summary>
    /// <remarks>
    /// Derived members must never reach the stored JSON — a stored copy could disagree with the
    /// members it came from. [JsonIgnore] does not inherit through an override, so every subtype
    /// must repeat it; the non-abstract members below are declared once and need it only here.
    /// </remarks>
    [JsonIgnore]
    public abstract DateTimeOffset StartTime { get; }

    /// <summary>The absolute instant, for ordering and windowing. What the StartTime column holds.</summary>
    [JsonIgnore]
    public DateTime StartTimeUtc => StartTime.UtcDateTime;

    /// <summary>The calendar day the athlete experienced, read at the captured offset.</summary>
    [JsonIgnore]
    public DateOnly LocalDate => DateOnly.FromDateTime(StartTime.DateTime);
}

public sealed record Instant(DateTimeOffset At) : Period
{
    [JsonIgnore]
    public override DateTimeOffset StartTime => At;
}

public sealed record Interval : Period
{
    public Interval(DateTimeOffset start, DateTimeOffset end)
    {
        // DateTimeOffset compares absolute instants, so endpoints captured at different offsets
        // order correctly without being normalized first.
        if (end < start) throw new ArgumentException("Interval end precedes start.", nameof(end));

        Start = start;
        End = end;
    }

    public DateTimeOffset Start { get; }
    public DateTimeOffset End { get; }

    [JsonIgnore]
    public TimeSpan Duration => End - Start;

    [JsonIgnore]
    public override DateTimeOffset StartTime => Start;
}
