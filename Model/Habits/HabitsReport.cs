using lionheart.Model.Analysis;

namespace lionheart.Model.Habits;

/// <summary>
/// How the athlete's days are shaped over a range: how much he sits and for how long at a time,
/// when in the day he moves and trains, how regular his sleep is — each split into weekdays and
/// weekends, and set against the same span before it.
/// </summary>
/// <remarks>
/// Built on the server from Oura's five-minute activity classification, the night's sleep periods
/// and the training log. Comparative throughout: the figure that means something is this span
/// against his own previous one, not against a population.
/// </remarks>
public sealed record HabitsReport(
    DayRange Range,
    IReadOnlyList<HabitsDay> Days,
    HabitsSummary Current,
    HabitsSummary Previous);

/// <summary>One day's habit figures, for the trends.</summary>
/// <param name="IsComplete">
/// Whether the day counts toward the summaries: a classification is stored for it, and no more than
/// three hours of it were off the finger or lying still outside a scored sleep. A day with the ring
/// on a table reads as a day of rest, and would otherwise count as one.
/// </param>
/// <param name="ActiveMinutes">Low, medium and high activity, from Oura's own per-minute totals.</param>
/// <param name="Bedtime">When the night ending this morning began — Oura's main sleep filed under this day.</param>
public sealed record HabitsDay(
    DateOnly Day,
    bool IsWeekend,
    bool IsComplete,
    int Steps,
    int ActiveMinutes,
    int SedentaryMinutes,
    int? LongestSedentaryMinutes,
    int LongSedentaryBouts,
    DateTimeOffset? Bedtime,
    DateTimeOffset? WakeTime);

/// <summary>A span's habits, for weekdays, for weekends, and for every day.</summary>
public sealed record HabitsSummary(
    DayRange Range,
    HabitsFigures Weekdays,
    HabitsFigures Weekends,
    HabitsFigures AllDays);

/// <summary>
/// The mean day across a set of days, and the sleep timing of the nights ending on them.
/// </summary>
/// <remarks>Every mean is over complete days only; a set with none has nothing to say and is absent.</remarks>
public sealed record HabitsFigures(
    int CompleteDays,
    double? Steps,
    double? ActiveMinutes,
    double? SedentaryMinutes,
    double? LongestSedentaryMinutes,
    double? LongSedentaryBouts,
    IReadOnlyList<HourOfDay> Hours,
    SleepTiming? Sleep);

/// <summary>
/// One clock hour of the mean day: minutes in each class, and how many training sessions began in it.
/// </summary>
/// <param name="Hour">The hour on the athlete's own clock, 0 to 23.</param>
/// <param name="Resting">Asleep, or lying still outside a scored sleep for half an hour or more.</param>
/// <param name="Sedentary">Waking sitting: inactive minutes and the brief rest inside them.</param>
/// <param name="SessionsStarted">Sessions begun in this hour across the span — a count, not a mean.</param>
public sealed record HourOfDay(
    int Hour,
    double Resting,
    double Sedentary,
    double Low,
    double Medium,
    double High,
    int SessionsStarted);

/// <summary>
/// When the athlete went to sleep and woke, and how much each varies night to night.
/// </summary>
/// <remarks>
/// Clock times are minutes after midnight on his own clock; a bedtime is measured from noon of the
/// evening before, so 23:30 and 00:30 are an hour apart rather than twenty-three. The spread is
/// the standard deviation in minutes — the plain measure of regularity.
/// </remarks>
public sealed record SleepTiming(
    int Nights,
    double BedtimeMinutesAfterNoon,
    double BedtimeSpreadMinutes,
    double WakeMinutesAfterMidnight,
    double WakeSpreadMinutes,
    double MidpointMinutesAfterMidnight);

/// <summary>One day as a timeline: its classified runs, its long sittings, its sleep and its training.</summary>
public sealed record HabitsDayDetail(
    HabitsDay Figures,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    IReadOnlyList<ActivityRun> Runs,
    IReadOnlyList<SedentaryBout> SedentaryBouts,
    IReadOnlyList<SleepSpan> Sleep,
    IReadOnlyList<TrainingSpan> Training);

/// <summary>Consecutive slots of one class.</summary>
/// <param name="IsAsleep">Inside a sleep period Oura scored, which is what separates a night's rest from a still afternoon.</param>
public sealed record ActivityRun(ActivityClass Class, DateTimeOffset Start, int Minutes, bool IsAsleep);

/// <summary>
/// An unbroken stretch of waking sitting, at least <see cref="MinimumMinutes"/> long.
/// </summary>
/// <remarks>
/// Broken by any five-minute slot of movement, by the ring coming off, by sleep, and by half an
/// hour or more of lying still. At five-minute resolution a stand-up shorter than a slot may not
/// register, so a bout here is an upper bound on the longest unbroken sitting rather than an exact
/// one.
/// </remarks>
public sealed record SedentaryBout(DateTimeOffset Start, int Minutes)
{
    public const int MinimumMinutes = 30;

    /// <summary>The length from which a sitting counts as a long one.</summary>
    public const int LongMinutes = 60;
}

public sealed record SleepSpan(DateTimeOffset Start, DateTimeOffset End, bool IsMainSleep);

/// <summary>A training session inside the day: logged in Lionheart, or a cardio workout the ring saw.</summary>
/// <param name="Activity">
/// Oura's activity name for a ring workout; for a logged session <c>strengthTraining</c>,
/// <c>trailRun</c> or <c>distanceAndTime</c>, by the sets it carries.
/// </param>
public sealed record TrainingSpan(string Activity, DateTimeOffset Start, DateTimeOffset End, bool IsLogged);
