using lionheart.Model.Oura.Readings;

namespace lionheart.Model.Cardio;

/// <summary>One stretch of a session with heart rate held at zone 4 or above: a work bout.</summary>
/// <param name="Minutes">The time the bout's samples stand for, not counting any brief dip inside it.</param>
public sealed record WorkBout(DateTimeOffset Start, double Minutes, int AverageBpm);

/// <summary>
/// The rule that reads a session's heart rate as work bouts: the structure a deliberate hard
/// session has and incidental high heart rate does not.
/// </summary>
/// <remarks>
/// <para>
/// A bout is heart rate at 80 % of maximum or above — zone 4 and 5, the threshold and VO2max range
/// that interval and tempo prescriptions are written in (Seiler 2010; Buchheit and Laursen 2013) —
/// held for at least two minutes. Two because heart rate takes one to two minutes to climb to a
/// work rate, so a shorter surge is a hill or a sprint for a bus rather than a bout, and because the
/// shortest common interval prescriptions run two to four minutes.
/// </para>
/// <para>
/// A dip below zone 4 of half a minute or less does not end a bout: a noisy sample or a turn on the
/// track is not a recovery. Samples more than a minute apart say nothing about what happened
/// between them, so a gap that long ends the bout and the sample before it stands for nothing: a
/// workout recording samples every few seconds, and a sparse background series — one reading every
/// few minutes — cannot be read as intervals at all.
/// </para>
/// </remarks>
public static class WorkBouts
{
    /// <summary>The lowest zone a bout is held in.</summary>
    public const int FloorZone = 4;

    public const double ShortestMinutes = 2;

    public static readonly TimeSpan LongestDip = TimeSpan.FromSeconds(30);

    public static readonly TimeSpan LongestGap = TimeSpan.FromMinutes(1);

    /// <summary>The bouts in a session's samples, oldest first.</summary>
    /// <param name="samples">The session's samples, oldest first.</param>
    /// <param name="end">The session's end, which the last sample's time runs to.</param>
    public static IReadOnlyList<WorkBout> Of(IReadOnlyList<HeartRateSample> samples, DateTimeOffset end, int maxBpm)
    {
        var bouts = new List<WorkBout>();
        Building? building = null;
        var dip = TimeSpan.Zero;

        void Close()
        {
            if (building is { Held: var length } held && length >= TimeSpan.FromMinutes(ShortestMinutes))
                bouts.Add(new WorkBout(held.Start, length.TotalMinutes, (int)Math.Round(held.BeatMinutes / length.TotalMinutes)));
            building = null;
            dip = TimeSpan.Zero;
        }

        for (var index = 0; index < samples.Count; index++)
        {
            var sample = samples[index];
            var next = index + 1 < samples.Count ? samples[index + 1].At : end;
            var toNext = next - sample.At;
            var stands = toNext > LongestGap || toNext < TimeSpan.Zero ? TimeSpan.Zero : toNext;
            var gapBefore = index > 0 ? sample.At - samples[index - 1].At : TimeSpan.Zero;

            if (building is not null && gapBefore > LongestGap) Close();

            if (ZoneMinutes.ZoneOf(sample.Bpm, maxBpm) >= FloorZone)
            {
                building = building is { } held
                    ? held with { Held = held.Held + stands, BeatMinutes = held.BeatMinutes + sample.Bpm * stands.TotalMinutes }
                    : new Building(sample.At, stands, sample.Bpm * stands.TotalMinutes);
                dip = TimeSpan.Zero;
            }
            else if (building is not null)
            {
                dip += stands;
                if (dip > LongestDip) Close();
            }
        }

        Close();
        return bouts;
    }

    private sealed record Building(DateTimeOffset Start, TimeSpan Held, double BeatMinutes);
}
