namespace lionheart.Model.Oura.Readings;

/// <summary>
/// An evenly-spaced run of samples the ring recorded across one period — the overnight heart-rate
/// and HRV curves, a session's motion count.
/// </summary>
/// <remarks>
/// The three members travel together because none of them means anything alone: a list of numbers
/// with no interval and no start instant cannot be placed on a clock. Oura sends exactly this
/// envelope for every series it exposes, which is why one type serves all of them.
/// <para>
/// <see cref="Samples"/> elements are nullable because a gap is real data, not a defect: the ring
/// records no value while it is off the finger or cannot get a reading, and roughly one overnight
/// slot in seven is empty. Collapsing those to zero would read as a heart rate of zero, and
/// dropping them would silently shift every later sample earlier in the night.
/// </para>
/// </remarks>
public sealed record OuraSampleSeries(
    double IntervalSeconds,
    IReadOnlyList<double?> Samples,
    DateTimeOffset StartedAt)
{
    // Every member is redeclared, not only the guarded one: System.Text.Json writes
    // non-redeclared positional members first, so guarding a subset would reorder the stored JSON.
    public double IntervalSeconds { get; init; } = IntervalSeconds;
    public IReadOnlyList<double?> Samples { get; init; } = Require.Present(Samples, nameof(Samples));
    public DateTimeOffset StartedAt { get; init; } = StartedAt;

    /// <summary>Where the sample at <paramref name="index"/> sits on the clock.</summary>
    public DateTimeOffset At(int index) => StartedAt.AddSeconds(IntervalSeconds * index);

    /// <summary>
    /// Two runs are the same run when they hold the same samples, not when they hold the same list.
    /// </summary>
    /// <remarks>
    /// A record's generated equality compares a collection member by reference, which would make
    /// this value object unequal to itself across a round trip through storage — the one place the
    /// comparison is worth anything. Written out rather than left to the generated version because
    /// the generated version is wrong here and silently so.
    /// </remarks>
    public bool Equals(OuraSampleSeries? other) =>
        other is not null
        && IntervalSeconds.Equals(other.IntervalSeconds)
        && StartedAt.Equals(other.StartedAt)
        && Samples.SequenceEqual(other.Samples);

    /// <remarks>
    /// Deliberately cheap: the sample count rather than its contents, which equal runs always
    /// agree on. These are written once and hashed far less often than they are compared.
    /// </remarks>
    public override int GetHashCode() => HashCode.Combine(IntervalSeconds, StartedAt, Samples.Count);
}
