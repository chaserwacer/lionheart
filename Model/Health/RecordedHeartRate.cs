using lionheart.Model.Oura.Readings;

namespace lionheart.Model.Health;

/// <summary>
/// The heart rate the app that recorded a workout wrote to Apple Health across it, oldest first.
/// </summary>
/// <remarks>
/// Read as the athlete's heart rate for that workout ahead of the ring's all-day series, since it
/// is usually a chest strap or a watch sampling every second or so against the ring's five minutes.
/// The samples are the same shape the cardio analysis reads the ring's in, so zones, averages and
/// coverage are computed one way whichever source a workout's heart rate came from; their
/// <see cref="HeartRateSample.Source"/> says <see cref="SampleSource"/>, since "what the ring was
/// doing" has no answer here.
/// <para>
/// Empty is <see cref="None"/>: the app wrote no heart rate, and the ring's series is read instead.
/// </para>
/// </remarks>
public sealed record RecordedHeartRate(IReadOnlyList<HeartRateSample> Samples)
{
    public const string SampleSource = "appleHealth";

    public IReadOnlyList<HeartRateSample> Samples { get; init; } = Require.Present(Samples, nameof(Samples));

    public static RecordedHeartRate None { get; } = new([]);

    public bool IsRecorded => Samples.Count > 0;

    public static RecordedHeartRate Of(IEnumerable<(DateTimeOffset At, int Bpm)> readings) =>
        new([.. readings.OrderBy(reading => reading.At).Select(reading => new HeartRateSample(reading.At, reading.Bpm, SampleSource))]);

    /// <summary>Compared by the samples it holds, not by the list holding them.</summary>
    public bool Equals(RecordedHeartRate? other) => other is not null && Samples.SequenceEqual(other.Samples);

    public override int GetHashCode() => Samples.Count;
}
