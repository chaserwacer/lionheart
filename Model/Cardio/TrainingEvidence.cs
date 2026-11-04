using System.Text.Json.Serialization;

namespace lionheart.Model.Cardio;

/// <summary>What a workout's heart rate says about whether it was training.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TrainingVerdict>))]
public enum TrainingVerdict
{
    [JsonStringEnumMemberName("training")] Training,

    /// <summary>Too little of it has heart rate behind it, or there is no maximum to draw zones from, to say.</summary>
    [JsonStringEnumMemberName("unread")] Unread,

    /// <summary>Heart rate across it, and it stayed below training.</summary>
    [JsonStringEnumMemberName("belowTraining")] BelowTraining,
}

/// <summary>
/// The rule that decides whether a workout the ring guessed at was training, from its heart rate.
/// </summary>
/// <remarks>
/// <para>
/// A detected game that was a few throws of a ball is short and sits in zone 1, a few minutes
/// brushing zone 2; a real game runs longer, with most of its time at zone 2 and above and
/// stretches in zone 3. Oura's own intensity field cannot tell them apart: it says "moderate" for
/// nearly everything it detects.
/// </para>
/// <para>
/// So a workout is training when its heart rate held zone 2 or above for long enough to be a zone-2
/// session, or zone 3 or above for long enough to be a short hard one — and did so for at least
/// half the time it was read, so a spike inside an hour of pottering does not carry the hour.
/// </para>
/// </remarks>
public static class TrainingEvidence
{
    /// <summary>The share of a workout's clock time that must have heart rate behind it to be judged.</summary>
    public const double CoverageFloor = 0.5;

    /// <summary>Minutes at zone 2 or above that make a sustained aerobic session.</summary>
    public const double SustainedMinutes = 20;

    /// <summary>Minutes at zone 3 or above that make a short hard session.</summary>
    public const double HardMinutes = 10;

    /// <summary>The share of the read minutes that must be at zone 2 or above.</summary>
    public const double TrainingShare = 0.5;

    public static TrainingVerdict Judge(SessionHeartRate heartRate) => heartRate switch
    {
        SessionHeartRate.Recorded { Zones: { } zones, Coverage: >= CoverageFloor } => Judge(zones),
        SessionHeartRate.Recorded or SessionHeartRate.NotRecorded => TrainingVerdict.Unread,
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private static TrainingVerdict Judge(ZoneMinutes zones)
    {
        var read = zones.BelowZones + zones.InZones;
        var aerobic = zones.Zone2 + zones.Zone3 + zones.Zone4 + zones.Zone5;
        var hard = zones.Zone3 + zones.Zone4 + zones.Zone5;

        return (aerobic >= SustainedMinutes || hard >= HardMinutes) && aerobic >= TrainingShare * read
            ? TrainingVerdict.Training
            : TrainingVerdict.BelowTraining;
    }
}
