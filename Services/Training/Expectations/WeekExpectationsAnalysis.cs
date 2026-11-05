using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Model.Training.Availability;
using lionheart.Model.Training.Balance;
using lionheart.Model.Training.Expectations;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.GoingIn;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Score;
using lionheart.Services.Training.Balance;

namespace lionheart.Services.Training.Expectations;

/// <summary>Everything a week's expectations are read from, already read.</summary>
/// <param name="CardioOf">The cardio of any seven-day window: the week's own, and the four before it for the targets.</param>
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
}

/// <summary>
/// A week's expectations as a function of the log, the cardio, the pins and the days available.
/// </summary>
/// <remarks>
/// Pure, so every target and every reason is tested without a database. Each volume target grows
/// from the four seven-day blocks before the window (<see cref="Progression"/>); sessions hold at
/// the committed three. The adjusted target is the raw one prorated to the window's available days
/// (<see cref="Progression.Prorated"/>). The blocks before are read as they were, days off
/// included: a week away lowers what the body is used to, and the ramp back is meant to follow it.
/// </remarks>
public static class WeekExpectationsAnalysis
{
    public static WeekExpectations Of(ExpectationInputs inputs, IReadOnlyList<FamilyPriority> priorities, DateOnly endingOn)
    {
        var window = VolumeTargets.WindowEndingOn(endingOn);
        var days = inputs.Availability.Within(window);
        return new WeekExpectations(
            window,
            days,
            Sessions(inputs.Log, inputs.Availability, window),
            Families(inputs.Log, days, window),
            Cardio(inputs.CardioOf, days, window),
            [.. priorities.Select(priority => priority.Lift is PriorityLift.Chosen chosen
                    ? LiftAimAnalysis.Of(inputs.Log, priority.Family, chosen, endingOn, inputs.GoingInOn(endingOn), days)
                    : null)
                .OfType<PriorityAim>()]);
    }

    /// <summary>The four seven-day blocks before a window, newest first: what its targets grow from.</summary>
    public static IEnumerable<DayRange> Before(DayRange window) =>
        Enumerable.Range(1, Progression.ChronicWeeks)
            .Select(block => new DayRange(window.From.AddDays(-7 * block), window.To.AddDays(-7 * block)));

    /// <summary>Sessions trained in a seven-day block against the committed three, prorated to its available days.</summary>
    public static Expectation Sessions(TrainingLog log, AvailabilityCalendar availability, DayRange block)
    {
        var raw = Progression.Fixed(ScoreRules.Sessions);
        return Expect(
            log.Between(block.From, block.To).Count(session => session.Trained),
            raw,
            ScoreRules.Sessions,
            availability.Within(block),
            ExpectationReasons.Committed(raw, "sessions"));
    }

    /// <summary>Push, pull and legs: hard sets in the window against targets grown from the blocks before.</summary>
    public static IReadOnlyList<FamilyExpectation> Families(TrainingLog log, AvailabilityCalendar days, DayRange window)
    {
        var history = TrainingVolumeAnalysis.Placed(log, DateOnly.MinValue, window.To).ToList();
        IReadOnlyList<FamilyCount> In(DayRange range) =>
            TrainingVolumeAnalysis.Families([.. history.Where(placed => placed.Day >= range.From && placed.Day <= range.To)], history);

        var before = Before(window).Select(In).ToList();
        return
        [
            .. In(window).Select(family =>
            {
                var raw = Progression.Of(
                    ScoreRules.FamilySets,
                    [.. before.Select(block => (double?)block.Single(earlier => earlier.Family == family.Family).HardSets)]);
                return new FamilyExpectation(
                    family.Family,
                    Expect(family.HardSets, raw, ScoreRules.FamilySets, days, ExpectationReasons.Grown(raw, "hard sets")),
                    family.LastTrained);
            })
        ];
    }

    private static CardioExpectations Cardio(Func<DayRange, CardioMeasure> cardioOf, AvailabilityCalendar days, DayRange window)
    {
        var measure = cardioOf(window);
        if (measure is not CardioMeasure.Measured measured) return new CardioExpectations(measure, []);

        var earlier = Before(window).Select(cardioOf).Select(block => block as CardioMeasure.Measured).ToList();
        ZoneExpectation Band(ZoneBand band, double minutes, Standard standard, Func<CardioMeasure.Measured, double> figure)
        {
            var raw = Progression.Of(standard, [.. earlier.Select(block => block is null ? (double?)null : figure(block))]);
            return new ZoneExpectation(band, Expect(minutes, raw, standard, days, ExpectationReasons.Grown(raw, "min")));
        }

        return new CardioExpectations(
            measure,
            [
                Band(ZoneBand.Zone2, measured.Zone2Minutes, ScoreRules.Zone2, block => block.Zone2Minutes),
                Band(ZoneBand.Quality, measured.QualityMinutes, ScoreRules.Quality, block => block.QualityMinutes),
            ]);
    }

    private static Expectation Expect(
        double done, WeeklyTarget raw, Standard standard, AvailabilityCalendar days, string basis)
    {
        var adjusted = Progression.Prorated(raw, standard, days.AvailableDays);
        return new Expectation(done, raw, adjusted, [basis, .. ExpectationReasons.Adjusted(adjusted, days)]);
    }
}
