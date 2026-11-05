using System.Globalization;
using lionheart.Model.Training.Availability;
using lionheart.Model.Training.Score;

namespace lionheart.Services.Training.Expectations;

/// <summary>Why an expectation is what it is, in the plain lines the screens and the chat show.</summary>
public static class ExpectationReasons
{
    /// <summary>"4-week average 5.5 hard sets, +10 %: 7", or which bound the ramp met.</summary>
    public static string Grown(WeeklyTarget target, string unit) => target.Chronic switch
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
    /// What the window's days did to the target: nothing when every day was available, and that it
    /// held when rounding up kept it where it was.
    /// </summary>
    public static IReadOnlyList<string> Adjusted(WeeklyTarget raw, WeeklyTarget adjusted, AvailabilityCalendar days)
    {
        var off = days.Days.Count - days.AvailableDays;
        var marked = $"{off} of {days.Days.Count} days marked non-training{Notes(days)}";
        return off switch
        {
            0 => [],
            _ when days.AvailableDays is 0 => [$"Nothing asked: all {days.Days.Count} days marked non-training."],
            _ when adjusted.Target < raw.Target => [$"Lowered to {Figure(adjusted.Target)}: {marked}."],
            _ => [$"Still {Figure(adjusted.Target)} with {marked}: the days left hold it."],
        };
    }

    /// <summary>" (travel)" from the notes the days off carry, each once; empty when none has one.</summary>
    private static string Notes(AvailabilityCalendar days)
    {
        var notes = days.Unavailable.Select(day => day.Note).Where(note => note.Length > 0).Distinct().ToList();
        return notes.Count is 0 ? "" : $" ({string.Join(", ", notes)})";
    }

    public static string Figure(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
