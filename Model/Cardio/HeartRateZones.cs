using System.Text.Json.Serialization;
using lionheart.Model.Oura.Readings;

namespace lionheart.Model.Cardio;

/// <summary>
/// The maximum heart rate the zones are drawn from, and where that number came from.
/// </summary>
/// <remarks>
/// Two sources and neither is a measurement of maximum: the age prediction is a population average
/// (Tanaka, 208 − 0.7 × age, about ±10 bpm for any one person), and the ring's own peak is read
/// through a finger sensor that under-reads during hard, jarring efforts. So the higher of the two
/// is taken. A heart rate the athlete actually reached cannot exceed his true maximum, which makes
/// an observed peak above the prediction real evidence; a peak below it is as likely the sensor or
/// the effort as the heart, and is reported without being used.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(AgePredicted), "agePredicted")]
[JsonDerivedType(typeof(Observed), "observed")]
public abstract record MaxHeartRate
{
    private protected MaxHeartRate() { }

    public abstract int Bpm { get; }

    /// <summary>The age prediction, with the ring's own peak where there was enough to read one.</summary>
    public sealed record AgePredicted(int Bpm, int Age, int? ObservedPeak) : MaxHeartRate
    {
        public override int Bpm { get; } = Bpm;
    }

    /// <summary>The ring's own peak, which exceeded the age prediction.</summary>
    public sealed record Observed(int Bpm, int AgePredictedBpm, int Age) : MaxHeartRate
    {
        public override int Bpm { get; } = Bpm;
    }

    /// <summary>The fewest cardio samples a peak is read from; fewer and one bad reading is the peak.</summary>
    public const int PeakMinimumSamples = 100;

    /// <summary>
    /// The percentile of cardio-session samples read as the peak, rather than the single highest,
    /// which on a finger sensor is as often a motion artefact as a heartbeat.
    /// </summary>
    public const double PeakPercentile = 0.99;

    public static int Predicted(int age) => (int)Math.Round(208 - 0.7 * age);

    /// <summary>The estimate, or nothing when the profile states no usable age.</summary>
    public static MaxHeartRate? Estimate(int age, int? observedPeak)
    {
        if (age is <= 0 or > 120) return null;

        var predicted = Predicted(age);
        return observedPeak is { } peak && peak > predicted
            ? new Observed(peak, predicted, age)
            : new AgePredicted(predicted, age, observedPeak);
    }

    /// <summary>The peak of a set of cardio samples, or nothing when there are too few to read one.</summary>
    public static int? PeakOf(IReadOnlyCollection<int> bpm)
    {
        if (bpm.Count < PeakMinimumSamples) return null;

        var ordered = bpm.Order().ToList();
        return ordered[(int)Math.Floor(PeakPercentile * (ordered.Count - 1))];
    }
}

/// <summary>
/// Minutes spent in each heart-rate zone, plus the minutes below the first.
/// </summary>
/// <remarks>
/// Five zones as percentages of maximum heart rate — 50, 60, 70, 80 and 90 per cent — the scheme
/// most watches and the Edwards load below are built on. Zone 2 is the 60–70 per cent band the
/// aerobic-base literature means by "zone 2"; without a lactate or gas-exchange test, where his
/// own first threshold falls inside or above it is not known.
/// </remarks>
public sealed record ZoneMinutes(double BelowZones, double Zone1, double Zone2, double Zone3, double Zone4, double Zone5)
{
    public static readonly ZoneMinutes None = new(0, 0, 0, 0, 0, 0);

    /// <summary>The lower edge of each zone, as a fraction of maximum heart rate.</summary>
    public static readonly IReadOnlyList<double> LowerEdges = [0.5, 0.6, 0.7, 0.8, 0.9];

    public double InZones => Zone1 + Zone2 + Zone3 + Zone4 + Zone5;

    /// <summary>
    /// Edwards' training impulse: each zone's minutes weighted by the zone's number.
    /// </summary>
    /// <remarks>
    /// The simplest heart-rate load that respects intensity — an hour in zone 2 is 120, half an
    /// hour in zone 4 is 120 — and it needs nothing beyond the zones themselves.
    /// </remarks>
    public double Load => Zone1 + 2 * Zone2 + 3 * Zone3 + 4 * Zone4 + 5 * Zone5;

    public static ZoneMinutes operator +(ZoneMinutes left, ZoneMinutes right) => new(
        left.BelowZones + right.BelowZones,
        left.Zone1 + right.Zone1,
        left.Zone2 + right.Zone2,
        left.Zone3 + right.Zone3,
        left.Zone4 + right.Zone4,
        left.Zone5 + right.Zone5);

    /// <summary>Which zone a rate falls in: 0 below the first, then 1 to 5.</summary>
    public static int ZoneOf(int bpm, int maxBpm) =>
        LowerEdges.Count(edge => bpm >= edge * maxBpm);

    /// <summary><paramref name="minutes"/> placed in zone <paramref name="zone"/>.</summary>
    public static ZoneMinutes InZone(int zone, double minutes) => zone switch
    {
        0 => None with { BelowZones = minutes },
        1 => None with { Zone1 = minutes },
        2 => None with { Zone2 = minutes },
        3 => None with { Zone3 = minutes },
        4 => None with { Zone4 = minutes },
        5 => None with { Zone5 = minutes },
        _ => throw new ArgumentOutOfRangeException(nameof(zone), zone, "Zones run from 0 to 5."),
    };
}

/// <summary>
/// Time attributed to each heart-rate sample across a window: what the zone sums and the coverage
/// figure are both built from.
/// </summary>
/// <remarks>
/// A sample stands for the time until the next one, capped at Oura's nominal five-minute interval.
/// The cap is what keeps a gap honest: an hour with the ring off the finger is an hour with no
/// reading, not an hour at whatever the last sample said.
/// </remarks>
public static class SampleTime
{
    public static readonly TimeSpan NominalInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Each sample with the minutes it stands for, given samples inside the window, oldest first.
    /// </summary>
    public static IEnumerable<(HeartRateSample Sample, double Minutes)> Attributed(
        IReadOnlyList<HeartRateSample> samples, DateTimeOffset windowEnd) =>
        samples.Select((sample, index) =>
        {
            var next = index + 1 < samples.Count ? samples[index + 1].At : windowEnd;
            var span = next - sample.At;
            return (sample, Math.Max(0, Math.Min(span.TotalMinutes, NominalInterval.TotalMinutes)));
        });

    /// <summary>
    /// The average rate across attributed samples, each weighted by the time it stands for — so a
    /// burst of fast samples does not outvote the longer stretch of slower ones beside it. The plain
    /// mean where no sample stands for any time.
    /// </summary>
    public static double AverageBpm(IReadOnlyList<(HeartRateSample Sample, double Minutes)> attributed)
    {
        var minutes = attributed.Sum(pair => pair.Minutes);
        return minutes > 0
            ? attributed.Sum(pair => pair.Sample.Bpm * pair.Minutes) / minutes
            : attributed.Average(pair => pair.Sample.Bpm);
    }
}
