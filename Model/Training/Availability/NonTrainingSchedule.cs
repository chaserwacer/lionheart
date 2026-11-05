using System.Text.Json.Serialization;
using lionheart.Functional;

namespace lionheart.Model.Training.Availability;

/// <summary>Which days a non-training day covers: one date, or a weekday every week from a date.</summary>
/// <remarks>
/// An open repeat and one with a last day are two cases rather than one with a nullable end,
/// because "every Sunday" and "every Sunday until the move" are different statements. A weekly
/// schedule names its first day rather than a bare weekday, so marking Sundays off today leaves the
/// Sundays already trained and scored as they were; the weekday is the first day's, never stored
/// beside it.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(OneDay), "oneDay")]
[JsonDerivedType(typeof(EveryWeek), "everyWeek")]
[JsonDerivedType(typeof(EveryWeekUntil), "everyWeekUntil")]
public abstract record NonTrainingSchedule
{
    private protected NonTrainingSchedule() { }

    /// <summary>The first day the schedule covers.</summary>
    [JsonIgnore]
    public abstract DateOnly First { get; }

    /// <summary>Whether the schedule repeats every week.</summary>
    [JsonIgnore]
    public bool Repeats => this is not OneDay;

    public abstract bool Covers(DateOnly day);

    public sealed record OneDay(DateOnly Day) : NonTrainingSchedule
    {
        [JsonIgnore]
        public override DateOnly First => Day;

        public override bool Covers(DateOnly day) => day == Day;
    }

    /// <summary>The weekday of <paramref name="From"/>, every week from it on.</summary>
    public sealed record EveryWeek(DateOnly From) : NonTrainingSchedule
    {
        [JsonIgnore]
        public override DateOnly First => From;

        public override bool Covers(DateOnly day) => day >= From && day.DayOfWeek == From.DayOfWeek;
    }

    /// <summary>The weekday of <see cref="From"/>, every week from it through <see cref="Until"/>.</summary>
    public sealed record EveryWeekUntil : NonTrainingSchedule
    {
        [JsonConstructor]
        private EveryWeekUntil(DateOnly from, DateOnly until)
        {
            From = from;
            Until = until;
        }

        public DateOnly From { get; }

        /// <summary>The last day the schedule can cover, included.</summary>
        public DateOnly Until { get; }

        [JsonIgnore]
        public override DateOnly First => From;

        public override bool Covers(DateOnly day) => day >= From && day <= Until && day.DayOfWeek == From.DayOfWeek;

        /// <summary>A bounded weekly schedule; one that ends before it starts is refused.</summary>
        public static Result<NonTrainingSchedule, Problem> Of(DateOnly from, DateOnly until) =>
            until < from
                ? Result<NonTrainingSchedule, Problem>.Failure(
                    new Problem.Invalid(nameof(Until), "A repeat ends on or after the day it starts."))
                : Result<NonTrainingSchedule, Problem>.Success(new EveryWeekUntil(from, until));
    }
}
