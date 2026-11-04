using System.Text.Json.Serialization;

namespace lionheart.Model.Cardio;

/// <summary>
/// What kind of effort an Oura workout was, read from the activity Oura named.
/// </summary>
/// <remarks>
/// The first analysis that branches on Oura's activity vocabulary, so the vocabulary gets a typed
/// set here rather than in storage: the stored workout keeps the string Oura sent, and a value Oura
/// adds lands in <see cref="Unrecognised"/> instead of costing a record. Most of what the ring
/// files is not training — housework and short auto-detected walks are four in five of the
/// athlete's workouts — which is the whole reason the set exists.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<WorkoutCategory>))]
public enum WorkoutCategory
{
    /// <summary>Sustained aerobic work: running, hiking, cycling, swimming, rowing and the like.</summary>
    [JsonStringEnumMemberName("endurance")] Endurance,

    /// <summary>A game or a discipline with intermittent hard efforts: basketball, martial arts, dance.</summary>
    [JsonStringEnumMemberName("sport")] Sport,

    /// <summary>A walk. Under <see cref="WorkoutCategories.SessionWalk"/> it is getting somewhere, not a session.</summary>
    [JsonStringEnumMemberName("walk")] Walk,

    [JsonStringEnumMemberName("strength")] Strength,

    /// <summary>Movement the ring caught that is not training: housework, yard work, stretching.</summary>
    [JsonStringEnumMemberName("incidental")] Incidental,

    /// <summary>Oura's own "other": an activity nobody named.</summary>
    [JsonStringEnumMemberName("unspecified")] Unspecified,

    /// <summary>An activity this application does not yet place.</summary>
    [JsonStringEnumMemberName("unrecognised")] Unrecognised,
}

/// <summary>How a ring workout came to be on the record, as Oura's <c>source</c> says.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RingEntry>))]
public enum RingEntry
{
    /// <summary>The athlete entered it himself.</summary>
    [JsonStringEnumMemberName("entered")] Entered,

    /// <summary>
    /// The ring proposed it, from movement, and the athlete approved it or left it standing. He
    /// approves these without reading them, so the name is a guess: "handball" is often a game of
    /// catch.
    /// </summary>
    [JsonStringEnumMemberName("detected")] Detected,
}

/// <summary>What a ring workout needs before it is a cardio session.</summary>
public abstract record RingAdmission
{
    private RingAdmission() { }

    /// <summary>A session on its name alone.</summary>
    public sealed record Session : RingAdmission;

    /// <summary>A session only if its heart rate shows training (<see cref="TrainingEvidence"/>).</summary>
    public sealed record OnHeartRate : RingAdmission;

    public sealed record Excluded(WorkoutExclusion Reason) : RingAdmission;
}

public static class WorkoutCategories
{
    /// <summary>
    /// The shortest walk read as a session rather than as getting from one place to another.
    /// </summary>
    /// <remarks>
    /// Measured against the athlete's own record: the ring's auto-detected walks have a median of
    /// 19 minutes, and fewer than a quarter reach 30. Thirty minutes is also the shortest bout the
    /// physical-activity guidelines count as a session of moderate exercise.
    /// </remarks>
    public static readonly TimeSpan SessionWalk = TimeSpan.FromMinutes(30);

    /// <summary>The category of an Oura activity name, as Oura spells it.</summary>
    public static WorkoutCategory Of(string activity) => activity switch
    {
        "running" or "hiking" or "cycling" or "swimming" or "rowing" or "elliptical"
            or "stairExercise" or "cardiovascularExercise" or "crossCountrySkiing" or "skating"
            or "inlineSkating" or "paddling" or "kayaking" or "canoeing" or "mountainBiking"
            or "indoorCycling" or "treadmill" => WorkoutCategory.Endurance,
        "basketball" or "volleyball" or "soccer" or "americanFootball" or "handball" or "tennis"
            or "badminton" or "tableTennis" or "squash" or "hockey" or "baseball" or "rugby"
            or "martialArts" or "boxing" or "dance" or "jumpingRope" or "climbing" or "surfing"
            or "skateboarding" or "snowboarding" or "downhillSkiing" or "trampoline"
            or "frisbee" => WorkoutCategory.Sport,
        "walking" => WorkoutCategory.Walk,
        "strengthTraining" or "weightlifting" or "kettlebell" or "crossTraining" or "pilates"
            or "yoga" or "functionalTraining" => WorkoutCategory.Strength,
        "houseWork" or "yardwork" or "stretching" or "meditation" => WorkoutCategory.Incidental,
        "other" => WorkoutCategory.Unspecified,
        _ => WorkoutCategory.Unrecognised,
    };

    /// <summary>How Oura's <c>source</c> reads: "manual" is the athlete's own entry, anything else the ring's proposal.</summary>
    public static RingEntry EntryOf(string source) => source is "manual" ? RingEntry.Entered : RingEntry.Detected;

    /// <summary>
    /// Whether a ring workout of this category, length and entry is a cardio session, needs its
    /// heart rate to show it was one, or is out.
    /// </summary>
    /// <remarks>
    /// A name the athlete typed is taken at its word, as his own log is. A name the ring guessed,
    /// or no name at all, has to be shown by the heart rate. Incidental movement is out whatever
    /// its heart rate did: carrying boxes upstairs is not zone 2.
    /// </remarks>
    public static RingAdmission AdmissionOf(WorkoutCategory category, TimeSpan duration, RingEntry entry) => category switch
    {
        WorkoutCategory.Strength => new RingAdmission.Excluded(WorkoutExclusion.Strength),
        WorkoutCategory.Incidental => new RingAdmission.Excluded(WorkoutExclusion.NotTraining),
        WorkoutCategory.Walk when duration < SessionWalk => new RingAdmission.Excluded(WorkoutExclusion.ShortWalk),
        WorkoutCategory.Endurance or WorkoutCategory.Sport or WorkoutCategory.Walk => entry switch
        {
            RingEntry.Entered => new RingAdmission.Session(),
            RingEntry.Detected => new RingAdmission.OnHeartRate(),
            _ => throw new System.Diagnostics.UnreachableException(),
        },
        WorkoutCategory.Unspecified or WorkoutCategory.Unrecognised => new RingAdmission.OnHeartRate(),
        _ => throw new System.Diagnostics.UnreachableException(),
    };
}
