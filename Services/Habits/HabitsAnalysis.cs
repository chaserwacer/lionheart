using lionheart.Model.Analysis;
using lionheart.Model.Habits;
using lionheart.Model.Oura;
using lionheart.Model.Oura.Readings;

namespace lionheart.Services.Habits;

/// <summary>Everything the habits report is computed from, already read.</summary>
/// <param name="Activity">The activity documents for the range and the equal span before it.</param>
/// <param name="SleepPeriods">Every sleep period over the same days, naps included — a nap is not a sitting.</param>
/// <param name="Training">Training sessions over the same days: logged ones and the ring's cardio.</param>
public sealed record HabitsInputs(
    DayRange Range,
    IReadOnlyList<OuraDailyActivity> Activity,
    IReadOnlyList<OuraSleepPeriod> SleepPeriods,
    IReadOnlyList<TrainingSpan> Training);

/// <summary>
/// The habits report as a function of what was read.
/// </summary>
/// <remarks>
/// Pure, like the cardio analysis, so the rules — what breaks a sitting, which days count, how a
/// bedtime either side of midnight is averaged — are tested without a database.
/// </remarks>
public static class HabitsAnalysis
{
    /// <summary>
    /// The most of a day that may be unaccounted for and still count toward a summary: three hours
    /// off the finger or lying unscored.
    /// </summary>
    public const int MaximumUnaccountedSlots = 36;

    /// <summary>
    /// The fewest slots a day's classification must hold to be a whole day: 23 hours.
    /// </summary>
    /// <remarks>
    /// A day still in progress is a short classification with nothing off the finger, which would
    /// otherwise count as a day with little sitting and few steps and drag the current span's means
    /// below the previous one's. Twenty-three hours rather than twenty-four lets in the day the
    /// clocks go forward.
    /// </remarks>
    public const int WholeDaySlots = 276;

    /// <summary>
    /// The shortest stretch of rest outside a scored sleep that is read as lying still rather than
    /// as a quiet moment in a sitting: half an hour.
    /// </summary>
    /// <remarks>
    /// Inside a sitting, Oura files a few still minutes as rest; a stretch this long is an unscored
    /// nap, a night the ring did not score, or the ring lying on a table — in the athlete's archive,
    /// whole days of nothing but rest with zero steps. None of those is sitting at a desk.
    /// </remarks>
    public const int LyingStillSlots = 6;

    /// <summary>The fewest nights a sleep timing is read from; fewer and one late night is the habit.</summary>
    public const int MinimumNights = 3;

    /// <summary>The span the report compares against: as long as the range, ending the day before it.</summary>
    public static DayRange PreviousOf(DayRange range) =>
        new(range.From.AddDays(-range.Days), range.From.AddDays(-1));

    public static HabitsReport Build(HabitsInputs inputs)
    {
        var previous = PreviousOf(inputs.Range);
        var mainSleeps = inputs.SleepPeriods
            .Where(period => period.Reading.IsMainSleep)
            .GroupBy(period => period.Day)
            .ToDictionary(day => day.Key, day => day.OrderBy(period => period.OuraId).First());

        var days = inputs.Activity
            .GroupBy(document => document.Day)
            .Select(day => day.OrderBy(document => document.OuraId).First())
            .OrderBy(document => document.Day)
            .Select(document => Read(document, inputs.SleepPeriods, mainSleeps.GetValueOrDefault(document.Day)))
            .ToList();

        return new HabitsReport(
            inputs.Range,
            [.. days.Where(day => Within(inputs.Range, day.Figures.Day)).Select(day => day.Figures)],
            Summarise(inputs.Range, days, mainSleeps.Values, inputs.Training),
            Summarise(previous, days, mainSleeps.Values, inputs.Training));
    }

    /// <summary>One day as a timeline, or nothing when no activity document is stored for it.</summary>
    public static HabitsDayDetail? Detail(DateOnly day, HabitsInputs inputs)
    {
        var document = inputs.Activity
            .Where(candidate => candidate.Day == day)
            .OrderBy(candidate => candidate.OuraId)
            .FirstOrDefault();
        if (document is null) return null;

        var main = inputs.SleepPeriods
            .Where(period => period.Reading.IsMainSleep && period.Day == day)
            .OrderBy(period => period.OuraId)
            .FirstOrDefault();
        var read = Read(document, inputs.SleepPeriods, main);
        var (startsAt, endsAt) = Bounds(document, main);

        return new HabitsDayDetail(
            read.Figures,
            startsAt,
            endsAt,
            Runs(read.Slots),
            Bouts(read.Slots),
            [.. inputs.SleepPeriods
                .Select(period => (Span: Span(period), period.Reading.IsMainSleep))
                .Where(period => period.Span is { } span && span.Start < endsAt && span.End > startsAt)
                .Select(period => new SleepSpan(period.Span!.Value.Start, period.Span.Value.End, period.IsMainSleep))
                .OrderBy(span => span.Start)],
            [.. inputs.Training
                .Where(session => session.Start < endsAt && session.End > startsAt)
                .OrderBy(session => session.Start)]);
    }

    // ---- One day ----

    /// <param name="IsAsleep">Inside a sleep period Oura scored.</param>
    /// <param name="IsLyingStill">Part of a stretch of rest outside scored sleep at least <see cref="LyingStillSlots"/> long.</param>
    private sealed record Slot(DateTimeOffset Start, ActivityClass Class, bool IsAsleep, bool IsLyingStill);

    private sealed record ReadDay(HabitsDay Figures, IReadOnlyList<Slot> Slots);

    private static ReadDay Read(
        OuraDailyActivity document, IReadOnlyList<OuraSleepPeriod> sleep, OuraSleepPeriod? mainSleep)
    {
        var reading = document.Reading;
        var (startsAt, endsAt) = Bounds(document, mainSleep);
        var overnight = sleep
            .Select(Span)
            .OfType<(DateTimeOffset Start, DateTimeOffset End)>()
            .Where(span => span.Start < endsAt && span.End > startsAt)
            .ToList();
        var slots = reading.Timeline is { } timeline
            ? WithLyingStill(
            [
                .. timeline.Slots().Select(slot =>
                    new Slot(slot.Start, slot.Class, IsAsleep(slot.Start, overnight), IsLyingStill: false))
            ])
            : [];
        var bouts = Bouts(slots);
        var night = mainSleep is null ? null : Span(mainSleep);

        return new ReadDay(
            new HabitsDay(
                document.Day,
                IsWeekend(document.Day),
                reading.Timeline is not null
                    && slots.Count >= WholeDaySlots
                    && slots.Count(slot => slot.Class is ActivityClass.NonWear || slot.IsLyingStill) <= MaximumUnaccountedSlots,
                reading.Steps,
                (reading.LowActivityTimeSeconds + reading.MediumActivityTimeSeconds + reading.HighActivityTimeSeconds) / 60,
                reading.SedentaryTimeSeconds / 60,
                bouts.Count is 0 ? (reading.Timeline is null ? null : 0) : bouts.Max(bout => bout.Minutes),
                bouts.Count(bout => bout.Minutes >= SedentaryBout.LongMinutes),
                night?.Start,
                night?.End),
            slots);
    }

    private static bool IsAsleep(DateTimeOffset instant, IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> sleep) =>
        sleep.Any(span => instant >= span.Start && instant < span.End);

    /// <summary>The slots with every long enough stretch of unscored rest marked as lying still.</summary>
    private static IReadOnlyList<Slot> WithLyingStill(IReadOnlyList<Slot> slots) =>
    [
        .. Grouped(slots, (previous, next) => IsUnscoredRest(previous) == IsUnscoredRest(next))
            .SelectMany(run => IsUnscoredRest(run[0]) && run.Count >= LyingStillSlots
                ? run.Select(slot => slot with { IsLyingStill = true })
                : run)
    ];

    private static bool IsUnscoredRest(Slot slot) => !slot.IsAsleep && slot.Class is ActivityClass.Rest;

    /// <summary>Consecutive slots of one class and one sleep state, merged.</summary>
    private static IReadOnlyList<ActivityRun> Runs(IReadOnlyList<Slot> slots) =>
    [
        .. Grouped(slots, (previous, next) => previous.Class == next.Class && previous.IsAsleep == next.IsAsleep)
            .Select(run => new ActivityRun(
                run[0].Class, run[0].Start, run.Count * ActivityTimeline.SlotMinutes, run[0].IsAsleep))
    ];

    /// <summary>Every waking sedentary stretch at least <see cref="SedentaryBout.MinimumMinutes"/> long.</summary>
    private static IReadOnlyList<SedentaryBout> Bouts(IReadOnlyList<Slot> slots) =>
    [
        .. Grouped(slots, (previous, next) => IsSitting(previous) == IsSitting(next))
            .Where(run => IsSitting(run[0]))
            .Select(run => new SedentaryBout(run[0].Start, run.Count * ActivityTimeline.SlotMinutes))
            .Where(bout => bout.Minutes >= SedentaryBout.MinimumMinutes)
    ];

    private static bool IsSitting(Slot slot) => !slot.IsAsleep && !slot.IsLyingStill && slot.Class.IsSedentary();

    /// <summary>The slots cut into runs wherever <paramref name="together"/> says two neighbours differ.</summary>
    private static IEnumerable<IReadOnlyList<Slot>> Grouped(IReadOnlyList<Slot> slots, Func<Slot, Slot, bool> together)
    {
        var start = 0;
        for (var index = 1; index <= slots.Count; index++)
        {
            if (index < slots.Count && together(slots[index - 1], slots[index])) continue;
            if (index > start) yield return [.. slots.Skip(start).Take(index - start)];
            start = index;
        }
    }

    /// <summary>
    /// Where the activity day sits on the clock: from its classification, or, for a day stored
    /// without one, 04:00 to 04:00 at the offset the night ending that morning was recorded at —
    /// the nearest record of where the athlete was — and UTC only when there is no night either.
    /// </summary>
    private static (DateTimeOffset StartsAt, DateTimeOffset EndsAt) Bounds(
        OuraDailyActivity document, OuraSleepPeriod? mainSleep)
    {
        if (document.Reading.Timeline is { } timeline)
        {
            return (timeline.StartedAt, timeline.SlotStart(timeline.Classes.Length));
        }

        var offset = mainSleep?.Period.StartTime.Offset ?? TimeSpan.Zero;
        var startsAt = new DateTimeOffset(document.Day.ToDateTime(new TimeOnly(4, 0)), offset);
        return (startsAt, startsAt.AddDays(1));
    }

    // ---- A span of days ----

    private static HabitsSummary Summarise(
        DayRange range,
        IReadOnlyList<ReadDay> days,
        IEnumerable<OuraSleepPeriod> mainSleeps,
        IReadOnlyList<TrainingSpan> training)
    {
        var inRange = days.Where(day => Within(range, day.Figures.Day)).ToList();
        var nights = mainSleeps.Where(night => Within(range, night.Day)).ToList();
        var sessions = training.Where(session => Within(range, LocalDay(session.Start))).ToList();

        HabitsFigures Figures(Func<DateOnly, bool> includes) => FiguresOf(
            [.. inRange.Where(day => includes(day.Figures.Day))],
            [.. nights.Where(night => includes(night.Day))],
            [.. sessions.Where(session => includes(LocalDay(session.Start)))]);

        return new HabitsSummary(
            range,
            Figures(day => !IsWeekend(day)),
            Figures(IsWeekend),
            Figures(_ => true));
    }

    private static HabitsFigures FiguresOf(
        IReadOnlyList<ReadDay> days, IReadOnlyList<OuraSleepPeriod> nights, IReadOnlyList<TrainingSpan> sessions)
    {
        var complete = days.Where(day => day.Figures.IsComplete).ToList();

        double? Mean(Func<HabitsDay, double?> figure)
        {
            var values = complete.Select(day => figure(day.Figures)).OfType<double>().ToList();
            return values.Count is 0 ? null : values.Average();
        }

        return new HabitsFigures(
            complete.Count,
            Mean(day => day.Steps),
            Mean(day => day.ActiveMinutes),
            Mean(day => day.SedentaryMinutes),
            Mean(day => day.LongestSedentaryMinutes),
            Mean(day => day.LongSedentaryBouts),
            Hours(complete, sessions),
            Timing(nights));
    }

    /// <summary>The mean complete day, hour by hour on the athlete's own clock.</summary>
    private static IReadOnlyList<HourOfDay> Hours(IReadOnlyList<ReadDay> complete, IReadOnlyList<TrainingSpan> sessions)
    {
        var slots = complete.SelectMany(day => day.Slots).ToLookup(slot => slot.Start.Hour);
        var started = sessions.ToLookup(session => session.Start.Hour);
        var dayCount = Math.Max(1, complete.Count);

        double Minutes(int hour, Func<Slot, bool> counts) =>
            slots[hour].Count(counts) * ActivityTimeline.SlotMinutes / (double)dayCount;

        return
        [
            .. Enumerable.Range(0, 24).Select(hour => new HourOfDay(
                hour,
                Minutes(hour, slot => slot.IsAsleep || slot.IsLyingStill),
                Minutes(hour, IsSitting),
                Minutes(hour, slot => !slot.IsAsleep && slot.Class is ActivityClass.Low),
                Minutes(hour, slot => !slot.IsAsleep && slot.Class is ActivityClass.Medium),
                Minutes(hour, slot => !slot.IsAsleep && slot.Class is ActivityClass.High),
                started[hour].Count()))
        ];
    }

    /// <summary>
    /// When the nights began and ended, or nothing with too few nights to call a habit.
    /// </summary>
    /// <remarks>
    /// Bedtimes are measured from noon so a set straddling midnight averages to the middle of it
    /// rather than to midday; wake times and the midpoint are measured from midnight, which no
    /// main sleep's end or middle realistically crosses.
    /// </remarks>
    public static SleepTiming? Timing(IReadOnlyList<OuraSleepPeriod> nights)
    {
        var spans = nights.Select(Span).OfType<(DateTimeOffset Start, DateTimeOffset End)>().ToList();
        if (spans.Count < MinimumNights) return null;

        var bedtimes = spans.Select(span => MinutesAfter(span.Start, noon: true)).ToList();
        var wakes = spans.Select(span => MinutesAfter(span.End, noon: false)).ToList();
        var midpoints = spans.Select(span => MinutesAfter(span.Start + (span.End - span.Start) / 2, noon: false)).ToList();

        return new SleepTiming(
            spans.Count,
            bedtimes.Average(),
            Spread(bedtimes),
            wakes.Average(),
            Spread(wakes),
            midpoints.Average());
    }

    private static double MinutesAfter(DateTimeOffset instant, bool noon)
    {
        var minutes = instant.Hour * 60 + instant.Minute + instant.Second / 60.0;
        return noon ? (minutes - 720 + 1440) % 1440 : minutes;
    }

    private static double Spread(IReadOnlyList<double> values)
    {
        var mean = values.Average();
        return Math.Sqrt(values.Sum(value => (value - mean) * (value - mean)) / (values.Count - 1));
    }

    // ---- Shared ----

    private static (DateTimeOffset Start, DateTimeOffset End)? Span(OuraSleepPeriod period) =>
        period.Period is Interval interval ? (interval.Start, interval.End) : null;

    private static bool IsWeekend(DateOnly day) => day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    private static DateOnly LocalDay(DateTimeOffset instant) => DateOnly.FromDateTime(instant.DateTime);

    private static bool Within(DayRange range, DateOnly day) => day >= range.From && day <= range.To;
}
