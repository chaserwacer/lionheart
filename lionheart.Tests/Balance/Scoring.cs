using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Model.Training.Availability;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.GoingIn;
using lionheart.Model.Training.Score;
using lionheart.Services.Training.Balance;
using lionheart.Services.Training.Expectations;
using lionheart.Services.Training.Score;

namespace lionheart.Tests.Balance;

/// <summary>
/// The expectation inputs the pure score, expectation and next-session tests read: a log, pins and
/// cardio, with every day open and no going-in rating unless a test says otherwise.
/// </summary>
internal static class Scoring
{
    /// <summary>No day marked non-training and nothing planned, over any range a window asks about.</summary>
    public static readonly AvailabilityCalendar EveryDayOpen = new(new DayRange(DateOnly.MinValue, DateOnly.MinValue), []);

    public static ExpectationInputs Inputs(
        TrainingLog log,
        IReadOnlyList<FocusPin> pins,
        Func<DayRange, CardioMeasure> cardioOf,
        AvailabilityCalendar? availability = null,
        IReadOnlyDictionary<DateOnly, GoingInRating>? goingIn = null) =>
        new(log, pins, cardioOf, availability ?? EveryDayOpen, goingIn ?? new Dictionary<DateOnly, GoingInRating>());

    public static TrainingScore Score(
        TrainingLog log,
        IReadOnlyList<FocusPin> pins,
        Func<DayRange, CardioMeasure> cardioOf,
        DateOnly endingOn,
        AvailabilityCalendar? availability = null,
        IReadOnlyDictionary<DateOnly, GoingInRating>? goingIn = null) =>
        TrainingScoreAnalysis.Score(Inputs(log, pins, cardioOf, availability, goingIn), endingOn);

    /// <summary>The days given marked non-training by one-off statements, every other day open.</summary>
    public static AvailabilityCalendar Off(params DateOnly[] days) =>
        new(
            new DayRange(days.Min(), days.Max()),
            [.. days.Order().Select(day => new DayAvailability.NonTraining(day, Guid.NewGuid(), "travel", Repeats: false))]);
}
