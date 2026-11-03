using System.Text.Json.Serialization;
using lionheart.Model.Analysis;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Score;
using lionheart.Model.Training.Sets;

namespace lionheart.Model.Training.Focus;

/// <summary>
/// What the athlete is striving for over the seven days ending on a day: each family's priority
/// lift with its progress and frequency, and the week's cardio against its targets.
/// </summary>
public sealed record PriorityReport(DayRange Window, IReadOnlyList<FamilyPriority> Families, CardioMeasure Cardio);

/// <summary>One family's priority lift, or that it has none to read.</summary>
public sealed record FamilyPriority(PatternFamily Family, PriorityLift Lift);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(NoLift), "none")]
[JsonDerivedType(typeof(Chosen), "chosen")]
public abstract record PriorityLift
{
    private protected PriorityLift() { }

    /// <summary>No priority pinned, and no compound lift of the family trained in the last eight weeks.</summary>
    public sealed record NoLift : PriorityLift;

    /// <param name="Series">The session bests of the last twelve weeks and the window, oldest first.</param>
    /// <param name="SessionsLastFourWeeks">Sessions with the lift in the 28 days ending on the window's end.</param>
    public sealed record Chosen(
        Guid MovementDataId,
        string Name,
        PriorityChoice Choice,
        LiftProgress Progress,
        int SessionsLastFourWeeks,
        IReadOnlyList<SessionBest> Series) : PriorityLift;
}

/// <summary>How the lift came to be the family's priority.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ByPin), "pinned")]
[JsonDerivedType(typeof(MostTrained), "mostTrained")]
public abstract record PriorityChoice
{
    private protected PriorityChoice() { }

    /// <summary>The athlete pinned it as the family's priority for a horizon covering the window's end.</summary>
    public sealed record ByPin(FocusPinView Pin) : PriorityChoice;

    /// <summary>
    /// Nothing pinned, so the family's most-trained compound lift of the last eight weeks stands in,
    /// which is what an unpinned block's priority was in practice.
    /// </summary>
    public sealed record MostTrained(int Sessions) : PriorityChoice;
}

/// <summary>
/// The lift's best estimated one-rep max in the window against its best of the twelve weeks
/// before, on system load where its weight adds to bodyweight.
/// </summary>
/// <remarks>Epley, sets of one to twelve reps only, in the unit the lift was last logged in.</remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(NotTrained), "notTrained")]
[JsonDerivedType(typeof(NoEstimate), "noEstimate")]
[JsonDerivedType(typeof(FirstBack), "firstBack")]
[JsonDerivedType(typeof(Compared), "compared")]
public abstract record LiftProgress
{
    private protected LiftProgress() { }

    /// <summary>Not trained in the window.</summary>
    public sealed record NotTrained : LiftProgress;

    /// <summary>Trained, but no set estimates a single: all past twelve reps, or at no load.</summary>
    public sealed record NoEstimate : LiftProgress;

    /// <summary>Trained in the window with nothing in the twelve weeks before to compare with.</summary>
    public sealed record FirstBack(double Best, WeightUnit Unit, bool OnSystemLoad) : LiftProgress;

    /// <param name="AllTimeBest">The best before the window across the whole log, for naming a lifetime best.</param>
    public sealed record Compared(
        double Best, double PriorBest, double AllTimeBest, WeightUnit Unit, bool OnSystemLoad) : LiftProgress
    {
        public double Change => Best - PriorBest;

        public bool IsNewBest => Best > PriorBest;

        public bool IsLifetimeBest => Best > AllTimeBest;
    }
}

/// <summary>One session's best estimated one-rep max on a lift.</summary>
public sealed record SessionBest(DateOnly Day, double EstimatedOneRepMax, double Load, int Reps);
