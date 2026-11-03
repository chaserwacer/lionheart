using System.Text.Json.Serialization;
using lionheart.Model.Oura.Readings;

namespace lionheart.Model.Habits;

/// <summary>
/// What Oura classified one five-minute slot of the day as.
/// </summary>
/// <remarks>
/// Oura's documented digits (OpenAPI 1.43, <c>PublicDailyActivity.class_5_min</c>): 0 non-wear,
/// 1 rest, 2 inactive, 3 low, 4 medium, 5 high. Checked against the athlete's own archive: summing
/// each digit's slots lands within a few minutes a band of Oura's per-minute time totals on the
/// same day, in that order. A digit outside the documented set reads as
/// <see cref="Unrecognised"/> rather than failing the day.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<ActivityClass>))]
public enum ActivityClass
{
    [JsonStringEnumMemberName("nonWear")] NonWear,

    /// <summary>Lying still: sleep, and resting awake.</summary>
    [JsonStringEnumMemberName("rest")] Rest,

    /// <summary>Awake and barely moving — sitting, standing still.</summary>
    [JsonStringEnumMemberName("inactive")] Inactive,

    [JsonStringEnumMemberName("low")] Low,
    [JsonStringEnumMemberName("medium")] Medium,
    [JsonStringEnumMemberName("high")] High,
    [JsonStringEnumMemberName("unrecognised")] Unrecognised,
}

public static class ActivityClasses
{
    public static ActivityClass Of(char digit) => digit switch
    {
        '0' => ActivityClass.NonWear,
        '1' => ActivityClass.Rest,
        '2' => ActivityClass.Inactive,
        '3' => ActivityClass.Low,
        '4' => ActivityClass.Medium,
        '5' => ActivityClass.High,
        _ => ActivityClass.Unrecognised,
    };

    /// <summary>
    /// Whether a slot is sedentary when the athlete is awake: sitting or lying still.
    /// </summary>
    /// <remarks>
    /// Rest counts as well as inactive, because Oura files the stillest few minutes of a sitting as
    /// rest: in the athlete's last year of weekdays, rest is 6–8% of the slots between 10:00 and
    /// 18:00, and counting inactive alone splits one sitting in two at each of them, halving the
    /// hour-long sittings found. Sleep, and a long stretch of rest that is lying down rather than
    /// sitting, are told apart by the caller, which knows when the athlete was asleep.
    /// </remarks>
    public static bool IsSedentary(this ActivityClass activity) => activity switch
    {
        ActivityClass.Rest or ActivityClass.Inactive => true,
        ActivityClass.NonWear or ActivityClass.Low or ActivityClass.Medium or ActivityClass.High
            or ActivityClass.Unrecognised => false,
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    /// <summary>The timeline's slots, decoded, each with where it starts on the clock.</summary>
    public static IEnumerable<(DateTimeOffset Start, ActivityClass Class)> Slots(this ActivityTimeline timeline) =>
        timeline.Classes.Select((digit, index) => (timeline.SlotStart(index), Of(digit)));
}
