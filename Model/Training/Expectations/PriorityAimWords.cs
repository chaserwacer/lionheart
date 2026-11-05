using System.Diagnostics;
using System.Globalization;
using lionheart.Model.Training.Sets;

namespace lionheart.Model.Training.Expectations;

/// <summary>A priority aim as the chat's lines state it, so the score's digest and next sessions say it alike.</summary>
public static class PriorityAimWords
{
    /// <summary>"top set 5 x 230 lb (e1RM 268; 12-week best 270)", with pounds beside kilograms.</summary>
    public static string Aim(LiftAim aim) => aim switch
    {
        LiftAim.Beat beat => string.Create(
            CultureInfo.InvariantCulture,
            $"top set {beat.Reps} x {Weight(beat.Load, beat.Unit)}{(beat.OnSystemLoad ? " system load" : "")} (e1RM {beat.EstimatedOneRepMax:0}; 12-week best {beat.BestToBeat:0})"),
        LiftAim.Hold hold => string.Create(
            CultureInfo.InvariantCulture,
            $"hold top set {hold.Reps} x {Weight(hold.Load, hold.Unit)}{(hold.OnSystemLoad ? " system load" : "")} (e1RM {hold.EstimatedOneRepMax:0})"),
        LiftAim.Establish => "work up to a top set of 5 to set an estimate",
        _ => throw new UnreachableException(),
    };

    /// <summary>"Bench Press top set 5 x 205 lb (…) — the reasons", one priority's whole line.</summary>
    public static string Line(PriorityAim priority) =>
        $"{priority.Name} {Aim(priority.Aim)} ({string.Join(" ", priority.Reasons)})";

    private static string Weight(double load, WeightUnit unit) => unit switch
    {
        WeightUnit.Pounds => string.Create(CultureInfo.InvariantCulture, $"{load:0.#} lb"),
        WeightUnit.Kilograms => string.Create(CultureInfo.InvariantCulture, $"{load:0.#} kg ({load * 2.20462:0} lb)"),
        _ => throw new UnreachableException(),
    };
}
