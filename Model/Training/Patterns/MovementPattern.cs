using System.Diagnostics;
using System.Text.Json.Serialization;

namespace lionheart.Model.Training.Patterns;

/// <summary>
/// The shape of the work a movement does: which way the load travels relative to the body, or
/// that it is a single-joint accessory.
/// </summary>
/// <remarks>
/// Strings on the wire and in storage, so a reordering here never re-reads a stored row as a
/// different pattern.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<MovementPattern>))]
public enum MovementPattern
{
    [JsonStringEnumMemberName("horizontalPush")] HorizontalPush,
    [JsonStringEnumMemberName("verticalPush")] VerticalPush,
    [JsonStringEnumMemberName("horizontalPull")] HorizontalPull,
    [JsonStringEnumMemberName("verticalPull")] VerticalPull,
    [JsonStringEnumMemberName("squat")] Squat,
    [JsonStringEnumMemberName("hinge")] Hinge,
    [JsonStringEnumMemberName("singleLeg")] SingleLeg,

    /// <summary>A single-joint accessory: a curl, a raise, an extension.</summary>
    [JsonStringEnumMemberName("isolation")] Isolation,

    /// <summary>Core work, carries, sleds: training that is none of the patterns above.</summary>
    [JsonStringEnumMemberName("other")] Other,
}

/// <summary>
/// Push, pull or legs: the three groups the athlete judges overall gym health by, and the unit a
/// priority lift is chosen for. Core is the family of work that belongs to none of them.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PatternFamily>))]
public enum PatternFamily
{
    [JsonStringEnumMemberName("push")] Push,
    [JsonStringEnumMemberName("pull")] Pull,
    [JsonStringEnumMemberName("legs")] Legs,
    [JsonStringEnumMemberName("core")] Core,
}

public static class MovementPatterns
{
    /// <summary>The seven compound patterns, in the order a lifter lists them.</summary>
    public static readonly IReadOnlyList<MovementPattern> Compound =
    [
        MovementPattern.HorizontalPush, MovementPattern.VerticalPush,
        MovementPattern.HorizontalPull, MovementPattern.VerticalPull,
        MovementPattern.Squat, MovementPattern.Hinge, MovementPattern.SingleLeg,
    ];

    /// <summary>The families a week's lifting is judged on. Core is counted but not judged.</summary>
    public static readonly IReadOnlyList<PatternFamily> Judged = [PatternFamily.Push, PatternFamily.Pull, PatternFamily.Legs];

    /// <summary>
    /// The family a compound pattern belongs to, or nothing for an isolation or other movement,
    /// whose family comes from the muscles it trains instead.
    /// </summary>
    public static PatternFamily? FamilyOf(MovementPattern pattern) => pattern switch
    {
        MovementPattern.HorizontalPush or MovementPattern.VerticalPush => PatternFamily.Push,
        MovementPattern.HorizontalPull or MovementPattern.VerticalPull => PatternFamily.Pull,
        MovementPattern.Squat or MovementPattern.Hinge or MovementPattern.SingleLeg => PatternFamily.Legs,
        MovementPattern.Isolation or MovementPattern.Other => null,
        _ => throw new UnreachableException(),
    };
}
