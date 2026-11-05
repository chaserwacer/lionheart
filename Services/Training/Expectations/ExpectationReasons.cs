using System.Globalization;
using lionheart.Model.Training.Availability;
using lionheart.Model.Training.Expectations;
using lionheart.Model.Training.Load;
using lionheart.Model.Training.Score;
using lionheart.Services.Training.Load;

namespace lionheart.Services.Training.Expectations;

/// <summary>Why an expectation is what it is, in the plain lines the screens and the chat show.</summary>
public static class ExpectationReasons
{
    /// <summary>"4-week average 5.5 hard sets, +10 %: 7", which bound the ramp met, or what a spike last week held it to.</summary>
    public static string Grown(WeeklyTarget target, string unit) => target.Growth switch
    {
        TargetGrowth.HeldAfterSteepWeek held =>
            $"Held at {Figure(target.Target)} {unit}: last week's {Figure(held.LastWeek.Acute)} was {Ratio(held.LastWeek)} the 4 weeks before it, so this week does not build on it.",
        TargetGrowth.LoweredAfterSpike lowered =>
            $"Lowered to {Figure(target.Target)} {unit}, the usual of the 4 weeks before last: last week's {Figure(lowered.LastWeek.Acute)} was {Ratio(lowered.LastWeek)} it.",
        TargetGrowth.Ramped => Ramped(target, unit),
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private static string Ramped(WeeklyTarget target, string unit) => target.Chronic switch
    {
        null => $"Committed {Figure(target.Committed)} {unit}: nothing in the four weeks before to grow from.",
        { } chronic when target.Target >= target.Committed =>
            $"Committed {Figure(target.Committed)} {unit}: the 4-week average of {Figure(chronic)}, +10 %, reaches it.",
        { } chronic when target.Target <= target.Floor && chronic * (1 + Progression.Ramp) < target.Floor =>
            $"Floor {Figure(target.Floor)} {unit}: the 4-week average of {Figure(chronic)}, +10 %, is under it.",
        { } chronic => $"4-week average {Figure(chronic)} {unit}, +10 %: {Figure(target.Target)}.",
    };

    /// <summary>A standard that does not grow.</summary>
    public static string Committed(WeeklyTarget target, string unit) => $"Committed {Figure(target.Committed)} {unit} a week.";

    /// <summary>
    /// What the window's days did to the target: nothing when every day was available and none
    /// eased it, and that it held when rounding up kept it where it was.
    /// </summary>
    public static IReadOnlyList<string> Adjusted(
        WeeklyTarget raw, WeeklyTarget adjusted, AvailabilityCalendar days, EverydayWeek everyday)
    {
        var off = days.Days.Count - days.AvailableDays;
        string?[] parts =
        [
            off > 0 ? $"{off} of {days.Days.Count} days marked non-training{Notes(days)}" : null,
            everyday.EasedDays > 0 ? $"{everyday.FarAboveDays} days far above your usual activity" : null,
        ];
        var causes = string.Join("; ", parts.OfType<string>());
        return causes switch
        {
            "" => [],
            _ when days.AvailableDays is 0 => [$"Nothing asked: all {days.Days.Count} days marked non-training."],
            _ when adjusted.Target < raw.Target => [$"Lowered to {Figure(adjusted.Target)}: {causes}."],
            _ => [$"Still {Figure(adjusted.Target)} with {causes}: the days left hold it."],
        };
    }

    /// <summary>What a suggested deload asks of a family, so following it reads as the plan.</summary>
    public static string Deloaded(WeeklyTarget raw, double deload) =>
        $"Deload week suggested: {Figure(deload)} hard sets is the plan, {LoadAnalysis.DeloadShare * 100:0} % of the {Figure(raw.Target)} a building week asks.";

    /// <summary>Why a family asks nothing this week, and what to hold it at instead.</summary>
    public static string Paused(ManagedInjury injury, WeeklyTarget raw) => raw.Chronic switch
    {
        { } chronic when chronic > 0 =>
            $"Paused while {injury.Name} is managed: hold it near {Figure(Math.Round(chronic))} hard sets a week, your 4-week average, rather than building on it.",
        _ => $"Paused while {injury.Name} is managed: keep it light rather than building on it.",
    };

    /// <summary>"1.6×", or that the four weeks before held nothing.</summary>
    public static string Ratio(LoadRatio.Compared ratio) =>
        ratio.Chronic > 0 ? $"{ratio.Ratio.ToString("0.0", CultureInfo.InvariantCulture)}×" : "a jump from nothing in";

    /// <summary>" (travel)" from the notes the days off carry, each once; empty when none has one.</summary>
    private static string Notes(AvailabilityCalendar days)
    {
        var notes = days.Unavailable.Select(day => day.Note).Where(note => note.Length > 0).Distinct().ToList();
        return notes.Count is 0 ? "" : $" ({string.Join(", ", notes)})";
    }

    public static string Figure(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
