using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Model.Training.Availability;
using lionheart.Model.Training.Balance;
using lionheart.Model.Training.Expectations;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.GoingIn;
using lionheart.Model.Training.Load;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Score;
using lionheart.Services.Training.Balance;
using lionheart.Services.Training.Load;

namespace lionheart.Services.Training.Expectations;

/// <summary>Everything a week's expectations are read from, already read.</summary>
/// <param name="CardioOf">The cardio of any seven-day window: the week's own, and the blocks before it for the targets.</param>
/// <param name="Availability">The days of every window asked about and of the blocks before them.</param>
/// <param name="GoingIn">The going-in rating on the days it was read — only ever today — for the priority aims.</param>
public sealed record ExpectationInputs(
    TrainingLog Log,
    IReadOnlyList<FocusPin> Pins,
    Func<DayRange, CardioMeasure> CardioOf,
    AvailabilityCalendar Availability,
    IReadOnlyDictionary<DateOnly, GoingInRating> GoingIn)
{
    public GoingInRating GoingInOn(DateOnly day) => GoingIn.GetValueOrDefault(day) ?? new GoingInRating.NotRated();

    /// <summary>The everyday load of the windows asked about and of the week before each; none read is none.</summary>
    public EverydayLoad Everyday { get; init; } = EverydayLoad.None;

    /// <summary>Every injury the athlete has logged, active or resolved.</summary>
    public IReadOnlyList<InjuryRecord> Injuries { get; init; } = [];
}

/// <summary>
/// A week's expectations as a function of the log, the cardio, the pins, the days available, the
/// everyday load and the injuries.
/// </summary>
/// <remarks>
/// Pure, so every target and every reason is tested without a database. Each volume target grows
/// from the four seven-day blocks before the window, held back after a spike in the last of them
/// (<see cref="Progression"/>); sessions hold at the committed three. The adjusted target is the
/// raw one prorated to the window's available days, less half a day for each day of everyday load
/// far above his usual once there are two (<see cref="Progression.Prorated"/>); then a suggested
/// deload asks each family for its share, and an active injury pauses the families it touches.
/// The raw targets never bend for any of it, so the score stays comparable week to week. The
/// blocks before are read as they were, days off included: a week away lowers what the body is
/// used to, and the ramp back is meant to follow it.
/// </remarks>
public static class WeekExpectationsAnalysis
{
    public static WeekExpectations Of(ExpectationInputs inputs, IReadOnlyList<FamilyPriority> priorities, DateOnly endingOn)
    {
        var window = VolumeTargets.WindowEndingOn(endingOn);
        var days = inputs.Availability.Within(window);
        var sets = HardSetCounts.Of(inputs.Log);
        var load = LoadAnalysis.Of(inputs, sets, endingOn);
        var injuries = InjuryManagement.Managed(inputs.Injuries, inputs.Log, sets, endingOn);
        return new WeekExpectations(
            window,
            days,
            Sessions(inputs.Log, inputs.Availability, window, load.Everyday),
            Families(inputs.Log, sets, days, window, load.Everyday, load.Lifting.Deload, injuries),
            Cardio(inputs.CardioOf, days, window, load.Everyday),
            [.. priorities.Select(priority => priority.Lift is PriorityLift.Chosen chosen
                    ? LiftAimAnalysis.Of(
                        inputs.Log, priority.Family, chosen, endingOn, inputs.GoingInOn(endingOn), days,
                        LiftAimAnalysis.WeekHolds(load.Lifting.Deload, injuries.FirstOrDefault(injury => injury.Families.Contains(priority.Family))))
                    : null)
                .OfType<PriorityAim>()],
            load,
            injuries,
            WeeklyAdviceAnalysis.Of(inputs, sets, load.Lifting.Deload, endingOn));
    }

    /// <summary>
    /// The blocks before a window, newest first: the four its targets grow from, and a fifth so the
    /// newest of them can be judged against the four before it.
    /// </summary>
    public static IEnumerable<DayRange> Before(DayRange window) =>
        Enumerable.Range(1, Progression.ChronicWeeks + 1)
            .Select(block => new DayRange(window.From.AddDays(-7 * block), window.To.AddDays(-7 * block)));

    /// <summary>
    /// The first day a window's expectations read: the blocks before the week that ended the Sunday
    /// before its calendar week, which the advice is read from.
    /// </summary>
    public static DateOnly Reach(DateOnly endingOn) =>
        Before(VolumeTargets.WindowEndingOn(LoadAnalysis.WeekStarting(endingOn).AddDays(-1))).Min(block => block.From);

    /// <summary>Sessions trained in a seven-day block against the committed three, prorated to its available days.</summary>
    public static Expectation Sessions(TrainingLog log, AvailabilityCalendar availability, DayRange block) =>
        Sessions(log, availability, block, EverydayWeek.None);

    private static Expectation Sessions(TrainingLog log, AvailabilityCalendar availability, DayRange block, EverydayWeek everyday)
    {
        var raw = Progression.Fixed(ScoreRules.Sessions);
        return Expect(
            log.Between(block.From, block.To).Count(session => session.Trained),
            raw,
            ScoreRules.Sessions,
            availability.Within(block),
            everyday,
            ExpectationReasons.Committed(raw, "sessions"));
    }

    /// <summary>
    /// Push, pull and legs for the window ending on a day, without the cardio the rest of the week
    /// reads: what weekly volume reads its families against, so it agrees with the score.
    /// </summary>
    public static IReadOnlyList<FamilyExpectation> Families(
        TrainingLog log, AvailabilityCalendar availability, EverydayLoad everyday, IReadOnlyList<InjuryRecord> injuries, DateOnly endingOn)
    {
        var window = VolumeTargets.WindowEndingOn(endingOn);
        var sets = HardSetCounts.Of(log);
        return Families(
            log,
            sets,
            availability.Within(window),
            window,
            LoadAnalysis.Everyday(everyday, window),
            LoadAnalysis.Deload(log, sets, endingOn),
            InjuryManagement.Managed(injuries, log, sets, endingOn));
    }

    /// <summary>
    /// Push, pull and legs: hard sets in the window against targets grown from the blocks before,
    /// fitted to the days, a deload and the injuries.
    /// </summary>
    private static IReadOnlyList<FamilyExpectation> Families(
        TrainingLog log,
        HardSetCounts sets,
        AvailabilityCalendar days,
        DayRange window,
        EverydayWeek everyday,
        Deload deload,
        IReadOnlyList<ManagedInjury> injuries)
    {
        var history = TrainingVolumeAnalysis.Placed(log, DateOnly.MinValue, window.To).ToList();
        var counts = TrainingVolumeAnalysis.Families([.. history.Where(placed => placed.Day >= window.From)], history);
        return
        [
            .. counts.Select(family =>
            {
                var raw = Progression.Of(
                    ScoreRules.FamilySets, [.. Before(window).Select(block => (double?)sets.Of(family.Family, block))]);
                var expected = Expect(family.HardSets, raw, ScoreRules.FamilySets, days, everyday, ExpectationReasons.Grown(raw, "hard sets"));
                var pausedBy = injuries.FirstOrDefault(injury => injury.Families.Contains(family.Family));
                return new FamilyExpectation(
                    family.Family,
                    (pausedBy, deload) switch
                    {
                        ({ } injury, _) => Paused(expected, injury),
                        (null, Deload.Suggested) => Deloaded(expected),
                        _ => expected,
                    },
                    family.LastTrained) { Paused = pausedBy is not null };
            }),
        ];
    }

    /// <summary>A family an injury touches asks nothing, so the adjusted score holds nothing against it.</summary>
    private static Expectation Paused(Expectation expected, ManagedInjury injury) => expected with
    {
        Adjusted = expected.Adjusted with { Target = 0, Committed = 0, Floor = 0 },
        Reasons = [.. expected.Reasons, ExpectationReasons.Paused(injury, expected.Raw)],
    };

    /// <summary>A suggested deload asks its share of the adjusted target, rounded up to a whole set.</summary>
    private static Expectation Deloaded(Expectation expected)
    {
        var deload = Math.Ceiling(LoadAnalysis.DeloadShare * expected.Adjusted.Target - 1e-9);
        return deload >= expected.Adjusted.Target
            ? expected
            : expected with
            {
                Adjusted = expected.Adjusted with { Target = deload },
                Reasons = [.. expected.Reasons, ExpectationReasons.Deloaded(expected.Adjusted, deload)],
            };
    }

    private static CardioExpectations Cardio(
        Func<DayRange, CardioMeasure> cardioOf, AvailabilityCalendar days, DayRange window, EverydayWeek everyday)
    {
        var measure = cardioOf(window);
        if (measure is not CardioMeasure.Measured measured) return new CardioExpectations(measure, []);

        var earlier = Before(window).Select(cardioOf).Select(block => block as CardioMeasure.Measured).ToList();
        ZoneExpectation Band(ZoneBand band, double minutes, Standard standard, Func<CardioMeasure.Measured, double> figure)
        {
            var raw = Progression.Of(standard, [.. earlier.Select(block => block is null ? (double?)null : figure(block))]);
            return new ZoneExpectation(band, Expect(minutes, raw, standard, days, everyday, ExpectationReasons.Grown(raw, "min")));
        }

        return new CardioExpectations(
            measure,
            [
                Band(ZoneBand.Zone2, measured.Zone2Minutes, ScoreRules.Zone2, block => block.Zone2Minutes),
                Band(ZoneBand.Quality, measured.QualityMinutes, ScoreRules.Quality, block => block.QualityMinutes),
            ]);
    }

    private static Expectation Expect(
        double done, WeeklyTarget raw, Standard standard, AvailabilityCalendar days, EverydayWeek everyday, string basis)
    {
        var adjusted = Progression.Prorated(raw, standard, Math.Max(0, days.AvailableDays - everyday.EasedDays));
        return new Expectation(done, raw, adjusted, [basis, .. ExpectationReasons.Adjusted(raw, adjusted, days, everyday)]);
    }
}
