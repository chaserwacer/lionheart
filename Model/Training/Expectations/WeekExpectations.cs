using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Model.Training.Availability;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Score;

namespace lionheart.Model.Training.Expectations;

/// <summary>
/// What the seven days ending on a day ask of the athlete, measurement by measurement: what he did,
/// the target the score reads, the same target fitted to the days he had, and why each is what it is.
/// </summary>
/// <remarks>
/// One answer every reader takes its targets from — both scores, weekly volume, next sessions and
/// the chat — so no screen states a target another disagrees with. Each target grows from his
/// recent weeks (<see cref="Progression"/>); the adjusted one is that target prorated to the days
/// of the window not marked non-training, so a week of travel asks for what the days left can hold.
/// </remarks>
/// <param name="Availability">The window's days, open, planned or non-training.</param>
/// <param name="Sessions">Completed sessions against the week's sessions.</param>
/// <param name="Priorities">Each family's priority lift with its next top set, for the families that have one.</param>
public sealed record WeekExpectations(
    DayRange Window,
    AvailabilityCalendar Availability,
    Expectation Sessions,
    IReadOnlyList<FamilyExpectation> Families,
    CardioExpectations Cardio,
    IReadOnlyList<PriorityAim> Priorities);

/// <summary>One measurement's expectation for the week.</summary>
/// <param name="Done">What the window holds so far.</param>
/// <param name="Raw">The target the training score reads, the same whatever days were available.</param>
/// <param name="Adjusted">
/// <paramref name="Raw"/> prorated to the window's available days, each figure rounded up to its step;
/// the same as it when every day was available.
/// </param>
/// <param name="Reasons">Why the raw target is what it is, then what the days changed, in words.</param>
public sealed record Expectation(double Done, WeeklyTarget Raw, WeeklyTarget Adjusted, IReadOnlyList<string> Reasons);

/// <summary>Push, pull or legs: hard sets over the window against the week's target.</summary>
public sealed record FamilyExpectation(PatternFamily Family, Expectation Sets, DateOnly? LastTrained);

/// <summary>Zone-2 and quality minutes against the week's targets, or why the minutes cannot be read.</summary>
/// <param name="Zones">Each band's expectation; empty when the measure is unknown.</param>
public sealed record CardioExpectations(CardioMeasure Measure, IReadOnlyList<ZoneExpectation> Zones);

public sealed record ZoneExpectation(ZoneBand Band, Expectation Minutes);
