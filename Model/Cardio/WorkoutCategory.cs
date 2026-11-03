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

    /// <summary>
    /// A walk. Whether it is a session or noise depends on its length — see
    /// <see cref="WorkoutCategories.IsCardioSession"/>.
    /// </summary>
    [JsonStringEnumMemberName("walk")] Walk,

    [JsonStringEnumMemberName("strength")] Strength,

    /// <summary>Movement the ring caught that is not training: housework, yard work, stretching.</summary>
    [JsonStringEnumMemberName("incidental")] Incidental,

    /// <summary>An activity this application does not yet place. Counted, never guessed at.</summary>
    [JsonStringEnumMemberName("unrecognised")] Unrecognised,
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
        "houseWork" or "yardwork" or "stretching" or "other" or "meditation" => WorkoutCategory.Incidental,
        _ => WorkoutCategory.Unrecognised,
    };

    /// <summary>
    /// Whether a workout of this category and length is cardio training rather than strength,
    /// incidental movement, or a walk too short to be one.
    /// </summary>
    public static bool IsCardioSession(WorkoutCategory category, TimeSpan duration) => category switch
    {
        WorkoutCategory.Endurance or WorkoutCategory.Sport => true,
        WorkoutCategory.Walk => duration >= SessionWalk,
        WorkoutCategory.Strength or WorkoutCategory.Incidental or WorkoutCategory.Unrecognised => false,
        _ => throw new System.Diagnostics.UnreachableException(),
    };
}
