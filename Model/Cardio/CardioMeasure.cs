using System.Text.Json.Serialization;

namespace lionheart.Model.Cardio;

/// <summary>
/// Cardio over the window as the heart-rate zones read it, or unknown.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Measured), "measured")]
[JsonDerivedType(typeof(Unknown), "unknown")]
public abstract record CardioMeasure
{
    private protected CardioMeasure() { }

    /// <param name="Zone2Minutes">Minutes at 60–70 % of maximum heart rate inside cardio sessions.</param>
    /// <param name="QualityMinutes">
    /// Minutes of work bouts at zone 4 or above in endurance sessions that held a session's worth of
    /// them (<see cref="QualityWork"/>) — the intensity the score credits.
    /// </param>
    /// <param name="QualitySessions">The sessions those minutes came from.</param>
    /// <param name="HeartRateDays">Days of the window with any heart rate from the ring, out of seven.</param>
    public sealed record Measured(
        double Zone2Minutes, double QualityMinutes, int QualitySessions, int Sessions, int HeartRateDays) : CardioMeasure;

    public sealed record Unknown(CardioUnknownReason Reason) : CardioMeasure;
}

[JsonConverter(typeof(JsonStringEnumConverter<CardioUnknownReason>))]
public enum CardioUnknownReason
{
    /// <summary>No heart rate from the ring on any day of the window, and none recorded with a session.</summary>
    [JsonStringEnumMemberName("noHeartRate")] NoHeartRate,

    /// <summary>No age on the profile to draw zones from.</summary>
    [JsonStringEnumMemberName("noMaximumHeartRate")] NoMaximumHeartRate,

    /// <summary>Most of the window's cardio session time has no heart rate behind it.</summary>
    [JsonStringEnumMemberName("sessionsUnrecorded")] SessionsUnrecorded,
}
