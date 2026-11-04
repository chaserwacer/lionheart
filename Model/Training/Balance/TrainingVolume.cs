using System.Text.Json.Serialization;
using lionheart.Model.Analysis;
using lionheart.Model.Training.Patterns;

namespace lionheart.Model.Training.Balance;

/// <summary>
/// The hard sets of seven days, by family, by pattern and by muscle group, each against its
/// weekly target, with the last day each was trained and what the log says is up next.
/// </summary>
/// <remarks>
/// A hard set is a working, drop or failure set of a lift; a warm-up never counts. Seven days
/// ending on a day rather than a calendar week, so the figure never empties on a Monday and reads
/// the same whichever day it is opened. A week view asks for the seven days ending on its Sunday.
/// </remarks>
/// <param name="Unplaced">
/// Movements whose sets could not be counted because nothing says what they train. Listed rather
/// than dropped, so a missing classification shows as one.
/// </param>
public sealed record TrainingVolume(
    DayRange Window,
    IReadOnlyList<FamilyVolume> Families,
    IReadOnlyList<PatternVolume> Patterns,
    IReadOnlyList<MuscleVolume> Muscles,
    IReadOnlyList<UnplacedMovement> Unplaced,
    UpNext UpNext);

/// <summary>Push, pull or legs over the window: every hard set of a movement in the family, counted once.</summary>
/// <param name="LastTrained">
/// The last day the family was trained, on or before the window's end; absent when never. A day
/// trains a family with any compound set of it, or <see cref="VolumeTargets.AccessoryDay"/> sets of
/// its accessory work, so two face pulls on a push day do not make it a pull day.
/// </param>
public sealed record FamilyVolume(PatternFamily Family, int HardSets, int Target, DateOnly? LastTrained);

/// <summary>One movement pattern's hard sets over the window.</summary>
/// <param name="Target">Absent for isolation and other work, which is counted toward muscles rather than judged as a pattern.</param>
public sealed record PatternVolume(MovementPattern Pattern, int HardSets, int? Target, DateOnly? LastTrained);

/// <summary>
/// One muscle group's sets over the window: a set of a movement that trains it as a primary mover
/// counts whole, as a secondary mover half.
/// </summary>
/// <param name="LastTrained">The last day it was trained as a primary mover.</param>
public sealed record MuscleVolume(MuscleGroup Muscle, int Direct, int Indirect, int Target, DateOnly? LastTrained)
{
    public double Sets => Direct + 0.5 * Indirect;
}

/// <summary>A movement with hard sets in the window and no classification.</summary>
public sealed record UnplacedMovement(Guid MovementDataId, string Name, int HardSets);

/// <summary>The weekly targets volume is read against.</summary>
/// <remarks>
/// From the athlete's own analysis of the dose-response evidence (Pelland et al. 2026): the first
/// four to eight direct sets per muscle a week buy most of what more would, so four is the floor
/// for a muscle and for each compound pattern, and eight — two sessions of a four-set family —
/// the target for a family.
/// </remarks>
public static class VolumeTargets
{
    public const int Family = 8;

    /// <summary>The fewest isolation or other sets of a family that make a day one that trained it.</summary>
    public const int AccessoryDay = 3;
    public const int Pattern = 4;
    public const int Muscle = 4;

    /// <summary>The days the volume window spans, ending on the day asked for.</summary>
    public const int WindowDays = 7;

    public static DayRange WindowEndingOn(DateOnly day) => new(day.AddDays(1 - WindowDays), day);
}

/// <summary>
/// What the log says comes next: the family or families trained longest ago, and anything that
/// honestly bears on the choice.
/// </summary>
/// <remarks>
/// One recommendation with its reason, never an instruction: families last trained on the same day
/// are named together, which is how an upper day followed by a lower day leaves push and pull up
/// next as one. Readiness is reported where it is unusual, beside the recommendation rather than
/// in place of it.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Train), "train")]
[JsonDerivedType(typeof(NothingLogged), "nothingLogged")]
public abstract record UpNext
{
    private protected UpNext() { }

    /// <param name="Families">The families due, longest since first; one to three of them.</param>
    /// <param name="LastTrained">When the due families were last trained; absent when never.</param>
    /// <param name="TrainedToday">Whether the window's last day already has hard sets, so this is the next session rather than today's.</param>
    public sealed record Train(
        IReadOnlyList<PatternFamily> Families,
        DateOnly? LastTrained,
        bool TrainedToday,
        ReadinessNote Readiness) : UpNext;

    /// <summary>No classified lifting in the history at all, so there is no order to read.</summary>
    public sealed record NothingLogged : UpNext;
}

/// <summary>The day's readiness where it bears on training, against the athlete's own usual range.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(NotRead), "notRead")]
[JsonDerivedType(typeof(Usual), "usual")]
[JsonDerivedType(typeof(Low), "low")]
public abstract record ReadinessNote
{
    private protected ReadinessNote() { }

    /// <summary>No readiness for the day, or too little history to have a usual range.</summary>
    public sealed record NotRead : ReadinessNote;

    /// <summary>Oura's readiness score inside or above his usual range.</summary>
    /// <param name="OuraScore">Oura's 0–100 rating of the day, not a measurement.</param>
    public sealed record Usual(int OuraScore) : ReadinessNote;

    /// <summary>Oura's readiness score under the tenth percentile of his last 90 days.</summary>
    /// <param name="OuraScore">Oura's 0–100 rating of the day, not a measurement.</param>
    public sealed record Low(int OuraScore, int UsualLow, int UsualHigh) : ReadinessNote;
}
