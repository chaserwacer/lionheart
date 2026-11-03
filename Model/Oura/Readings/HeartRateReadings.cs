namespace lionheart.Model.Oura.Readings;

/// <summary>
/// One heart-rate sample from Oura's all-day series: when, how fast, and what the ring was doing.
/// </summary>
/// <remarks>
/// <see cref="Source"/> stays the string Oura sent — <c>awake</c>, <c>rest</c>, <c>sleep</c>,
/// <c>workout</c>, <c>session</c>, <c>live</c> in its published schema — for the reason every Oura
/// vocabulary here does: it is an external set Oura extends on its own schedule, and the analyses
/// window samples by a workout's own clock time rather than branching on it.
/// </remarks>
public sealed record HeartRateSample(DateTimeOffset At, int Bpm, string Source)
{
    public DateTimeOffset At { get; init; } = At;
    public int Bpm { get; init; } = Bpm;
    public string Source { get; init; } = Require.Present(Source, nameof(Source));
}

/// <summary>
/// Every heart-rate sample Oura holds for one UTC calendar day, oldest first.
/// </summary>
/// <remarks>
/// One run per day rather than a row per sample, the same choice as the overnight curves on a
/// sleep period: the samples are only ever read as a span, and a row each would be several hundred
/// rows a day bought for nothing. The day is UTC because the samples arrive as UTC instants with no
/// local offset beside them, so a UTC day is the one boundary that can be drawn without guessing
/// where the athlete was; readers ask for a window of instants and never see the day.
/// <para>
/// Unlike the overnight curves this run is not evenly spaced. Oura's nominal interval is five
/// minutes, but it samples faster during a workout and leaves gaps while the ring is off, so each
/// sample carries its own instant.
/// </para>
/// </remarks>
public sealed record HeartRateDayReading(IReadOnlyList<HeartRateSample> Samples)
{
    public IReadOnlyList<HeartRateSample> Samples { get; init; } = Require.Present(Samples, nameof(Samples));

    /// <summary>Compared by the samples it holds, not by the list holding them.</summary>
    /// <remarks>See <see cref="OuraSampleSeries.Equals(OuraSampleSeries?)"/> for why.</remarks>
    public bool Equals(HeartRateDayReading? other) =>
        other is not null && Samples.SequenceEqual(other.Samples);

    public override int GetHashCode() => Samples.Count;
}
