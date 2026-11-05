using lionheart.Model.Analysis;
using lionheart.Model.Habits;
using lionheart.Model.Oura.Readings;
using lionheart.Model.Training.Load;

namespace lionheart.Services.Training.Load;

/// <summary>One Oura activity day as everyday load reads it: its five-minute slots and how long the ring was off.</summary>
public sealed record ActivityDay(DateOnly Day, ActivityTimeline Timeline, int NonWearSeconds);

/// <summary>A stretch of the clock a training session covered.</summary>
public sealed record TrainingSpan(DateTimeOffset Start, DateTimeOffset End);

/// <summary>
/// Everyday load as a function of the ring's activity days and the training sessions in them.
/// </summary>
/// <remarks>
/// <para>
/// Pure, so the rule is tested without a database. A day's figure is the MET-minutes of every
/// five-minute slot Oura classed as low, medium or high activity that no training session covered,
/// each slot weighted by its band: 0.75, 3.5 and 7 MET-minutes a minute above rest — the middle of
/// Oura's low (1.5–3 MET), medium (3–6) and high (6 and over) bands, less the resting one, which is
/// what Oura's own MET-minute totals divided by each band's time come to. Slots rather than Oura's
/// day totals, because only the slots say when, and training has to be taken out.
/// </para>
/// <para>
/// Each day is judged against the 90 days before it: their median is his usual day, their 90th
/// percentile the top of his ordinary days. A day over the 90th percentile is above his usual; one
/// over it by a fifth again is far above. Only complete days build the usual — the ring off for no
/// more than three hours, the habits report's allowance — and not today, which is still going.
/// </para>
/// </remarks>
public static class EverydayLoadAnalysis
{
    /// <summary>The days before a day its usual is drawn from.</summary>
    public const int UsualDays = 90;

    /// <summary>The fewest complete days that make a usual: four weeks.</summary>
    public const int FewestUsualDays = 28;

    /// <summary>The most a day's ring may be off and still count toward his usual.</summary>
    public const int MostNonWearSeconds = 3 * 3600;

    /// <summary>The percentile of his days above which a day is above his usual.</summary>
    public const double HighPercentile = 0.9;

    /// <summary>
    /// How far over that a day must be to be far above: a fifth again — about the top 2 % of an
    /// active person's days, where a day on his feet from morning to night lands. Half again was
    /// tried and missed exactly that day.
    /// </summary>
    public const double FarAboveFactor = 1.2;

    public const double LowMetMinutesPerMinute = 0.75;
    public const double MediumMetMinutesPerMinute = 3.5;
    public const double HighMetMinutesPerMinute = 7;

    /// <summary>Each activity day of the range with its everyday load, judged against the days before it.</summary>
    /// <param name="days">The activity days of the range and of the 90 days before it.</param>
    /// <param name="today">The athlete's today, whose day is still going and builds no usual.</param>
    public static EverydayLoad Of(IReadOnlyList<ActivityDay> days, IReadOnlyList<TrainingSpan> training, DayRange range, DateOnly today)
    {
        var figures = days
            .Select(day => (day.Day, MetMinutes: MetMinutes(day.Timeline, training), Complete: day.NonWearSeconds <= MostNonWearSeconds && day.Day < today))
            .OrderBy(day => day.Day)
            .ToList();

        return new EverydayLoad(
        [
            .. figures
                .Where(day => day.Day >= range.From && day.Day <= range.To)
                .Select(day => new EverydayDay(
                    day.Day,
                    day.MetMinutes,
                    Standing(day.MetMinutes, [.. figures
                        .Where(before => before.Complete && before.Day < day.Day && before.Day >= day.Day.AddDays(-UsualDays))
                        .Select(before => before.MetMinutes)]))),
        ]);
    }

    /// <summary>The MET-minutes of the day's active slots that no training session covered.</summary>
    public static double MetMinutes(ActivityTimeline timeline, IReadOnlyList<TrainingSpan> training)
    {
        var end = timeline.SlotStart(timeline.Classes.Length);
        var spans = training.Where(span => span.Start < end && span.End > timeline.StartedAt).ToList();
        return timeline.Slots()
            .Where(slot => !spans.Any(span => span.Start < slot.Start.AddMinutes(ActivityTimeline.SlotMinutes) && span.End > slot.Start))
            .Sum(slot => ActivityTimeline.SlotMinutes * PerMinute(slot.Class));
    }

    private static double PerMinute(ActivityClass activity) => activity switch
    {
        ActivityClass.Low => LowMetMinutesPerMinute,
        ActivityClass.Medium => MediumMetMinutesPerMinute,
        ActivityClass.High => HighMetMinutesPerMinute,
        ActivityClass.NonWear or ActivityClass.Rest or ActivityClass.Inactive or ActivityClass.Unrecognised => 0,
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private static EverydayStanding Standing(double metMinutes, IReadOnlyList<double> before)
    {
        if (before.Count < FewestUsualDays) return new EverydayStanding.Unjudged();

        var sorted = before.Order().ToList();
        var usual = Percentile(sorted, 0.5);
        var high = Percentile(sorted, HighPercentile);
        var band = metMinutes switch
        {
            _ when metMinutes > high * FarAboveFactor => EverydayBand.FarAbove,
            _ when metMinutes > high => EverydayBand.Above,
            _ => EverydayBand.Usual,
        };
        return new EverydayStanding.Judged(usual, high, band);
    }

    /// <summary>Nearest-rank percentile of a sorted list, the rule the readiness notes use.</summary>
    private static double Percentile(IReadOnlyList<double> sorted, double fraction) =>
        sorted[(int)Math.Floor(fraction * (sorted.Count - 1))];
}
