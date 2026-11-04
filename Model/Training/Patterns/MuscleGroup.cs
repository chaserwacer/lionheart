using System.Diagnostics;
using System.Text.Json.Serialization;

namespace lionheart.Model.Training.Patterns;

/// <summary>
/// The muscle groups weekly volume is counted against: fine enough to see that pressing pays
/// triceps and pulling pays biceps, coarse enough that every movement in the log can be placed.
/// </summary>
/// <remarks>
/// A fixed vocabulary rather than athlete-defined names, because volume per muscle is compared
/// across movements and over years, and two spellings of one muscle would split its sets.
/// Traps count as upper back and the hip abductors as glutes.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<MuscleGroup>))]
public enum MuscleGroup
{
    [JsonStringEnumMemberName("chest")] Chest,
    [JsonStringEnumMemberName("frontDelts")] FrontDelts,
    [JsonStringEnumMemberName("sideDelts")] SideDelts,
    [JsonStringEnumMemberName("rearDelts")] RearDelts,
    [JsonStringEnumMemberName("triceps")] Triceps,
    [JsonStringEnumMemberName("lats")] Lats,
    [JsonStringEnumMemberName("upperBack")] UpperBack,
    [JsonStringEnumMemberName("biceps")] Biceps,
    [JsonStringEnumMemberName("forearms")] Forearms,
    [JsonStringEnumMemberName("quads")] Quads,
    [JsonStringEnumMemberName("hamstrings")] Hamstrings,
    [JsonStringEnumMemberName("glutes")] Glutes,
    [JsonStringEnumMemberName("adductors")] Adductors,
    [JsonStringEnumMemberName("calves")] Calves,
    [JsonStringEnumMemberName("lowerBack")] LowerBack,
    [JsonStringEnumMemberName("abs")] Abs,
}

public static class MuscleGroups
{
    public static readonly IReadOnlyList<MuscleGroup> All = Enum.GetValues<MuscleGroup>();

    /// <summary>
    /// The family a muscle is trained in. The lower back goes with legs because the hinges train
    /// it; the abs with core.
    /// </summary>
    public static PatternFamily FamilyOf(MuscleGroup muscle) => muscle switch
    {
        MuscleGroup.Chest or MuscleGroup.FrontDelts or MuscleGroup.SideDelts or MuscleGroup.Triceps
            => PatternFamily.Push,
        MuscleGroup.RearDelts or MuscleGroup.Lats or MuscleGroup.UpperBack or MuscleGroup.Biceps
            or MuscleGroup.Forearms => PatternFamily.Pull,
        MuscleGroup.Quads or MuscleGroup.Hamstrings or MuscleGroup.Glutes or MuscleGroup.Adductors
            or MuscleGroup.Calves or MuscleGroup.LowerBack => PatternFamily.Legs,
        MuscleGroup.Abs => PatternFamily.Core,
        _ => throw new UnreachableException(),
    };
}
