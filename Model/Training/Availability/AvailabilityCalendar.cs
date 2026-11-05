using System.Text.Json.Serialization;
using lionheart.Model.Analysis;

namespace lionheart.Model.Training.Availability;

/// <summary>
/// What a day is for training: nothing said about it, a session planned, a session trained, or
/// marked non-training.
/// </summary>
/// <remarks>
/// A day trained was available, whatever was said about it: otherwise marking a day off and
/// training on it anyway would lower the week's targets and keep the work, which is a game, not an
/// adjustment. Among the statements the more particular wins: a date marked off beats a session
/// planned on it, and a session planned on a date beats a weekday marked off every week — planning
/// a Sunday session is a decision about that Sunday, and marking that one date off is a decision
/// about the plan.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Open), "open")]
[JsonDerivedType(typeof(Planned), "planned")]
[JsonDerivedType(typeof(Trained), "trained")]
[JsonDerivedType(typeof(NonTraining), "nonTraining")]
public abstract record DayAvailability
{
    private protected DayAvailability() { }

    public abstract DateOnly Day { get; }

    [JsonIgnore]
    public bool IsAvailable => this is not NonTraining;

    /// <summary>Nothing said about the day: free to train.</summary>
    public sealed record Open(DateOnly Day) : DayAvailability
    {
        public override DateOnly Day { get; } = Day;
    }

    /// <summary>A planned session is dated for the day, so the day is spoken for training.</summary>
    public sealed record Planned(DateOnly Day) : DayAvailability
    {
        public override DateOnly Day { get; } = Day;
    }

    /// <summary>A session was completed on the day, so it was a training day.</summary>
    public sealed record Trained(DateOnly Day) : DayAvailability
    {
        public override DateOnly Day { get; } = Day;
    }

    /// <param name="NonTrainingDayId">The statement that covers the day, which removing it removes.</param>
    /// <param name="Repeats">Whether that statement is a weekly one, so removing it frees more than this day.</param>
    public sealed record NonTraining(DateOnly Day, Guid NonTrainingDayId, string Note, bool Repeats) : DayAvailability
    {
        public override DateOnly Day { get; } = Day;
    }
}

/// <summary>Every day of a range as training availability reads it, oldest first.</summary>
public sealed record AvailabilityCalendar(DayRange Range, IReadOnlyList<DayAvailability> Days)
{
    /// <summary>The days of the range free to train: every day not marked non-training.</summary>
    public int AvailableDays => Days.Count(day => day.IsAvailable);

    [JsonIgnore]
    public IReadOnlyList<DayAvailability.NonTraining> Unavailable => [.. Days.OfType<DayAvailability.NonTraining>()];

    /// <summary>The days of a sub-range; a day this availability does not hold reads as open.</summary>
    public AvailabilityCalendar Within(DayRange range) => new(
        range,
        [.. Enumerable.Range(0, range.Days)
            .Select(range.From.AddDays)
            .Select(day => Days.FirstOrDefault(known => known.Day == day) ?? new DayAvailability.Open(day))]);
}
